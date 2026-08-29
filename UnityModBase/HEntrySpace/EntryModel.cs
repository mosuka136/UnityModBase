using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityModBase.HEntrySpace
{
    /// <summary>
    /// 提供配置项和实时控制项共享的类型识别、键校验及值比较规则。
    /// 配置空间（<see cref="HConfigSpace.ConfigFileModel"/> 的编解码分发）、
    /// 控制空间（<see cref="HControlSpace.ControlService"/> 的绑定校验）和通用 GUI（变更判断）
    /// 都以本类为唯一规则来源，保证两条条目管线对“什么是合法条目值、两个值是否等价”给出一致答案。
    /// 全部方法为纯函数，不维护共享状态，可任意并发调用。
    /// </summary>
    public static class EntryModel
    {
        /// <summary>
        /// 判断类型是否属于共享条目值模型，可用于单值条目或多泛型条目的元素。
        /// </summary>
        /// <param name="type">待判断类型。</param>
        /// <returns>类型是自定义条目值、基础类型、枚举、集合、元组或有效多泛型条目值时返回 <c>true</c>。</returns>
        public static bool IsEntryValueType(Type type)
        {
            if (type == null)
                return false;
            if (typeof(IEntryValue).IsAssignableFrom(type))
                return true;
            return IsPrimitiveType(type)
                || IsEnumType(type)
                || IsCollectionType(type)
                || IsTupleType(type)
                || IsEntryMultipleValueType(type);
        }

        /// <summary>
        /// 判断类型是否属于条目模型共享的基础类型。
        /// 基础类型指 CLR 原始类型外加 <see cref="string"/>。
        /// </summary>
        /// <param name="type">待判断类型。</param>
        /// <returns>属于基础类型时返回 <c>true</c>；<paramref name="type"/> 为 <c>null</c> 时返回 <c>false</c>。</returns>
        public static bool IsPrimitiveType(Type type)
        {
            return type != null && (type.IsPrimitive || type == typeof(string));
        }

        /// <summary>判断类型是否为枚举。</summary>
        public static bool IsEnumType(Type type)
        {
            return type != null && type.IsEnum;
        }

        /// <summary>
        /// 判断类型是否可作为集合编码：数组、<see cref="IEnumerable{T}"/> 本身，或实现该接口的类型。
        /// 仅检查泛型可枚举接口，非泛型 <see cref="IEnumerable"/> 实现不被视为集合。
        /// </summary>
        /// <param name="type">待判断类型。</param>
        /// <returns>可按集合处理时返回 <c>true</c>；<paramref name="type"/> 为 <c>null</c> 时返回 <c>false</c>。</returns>
        public static bool IsCollectionType(Type type)
        {
            if (type == null)
                return false;
            if (type.IsArray)
                return true;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return true;
            return type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        /// <summary>
        /// 获取集合类型的元素类型。
        /// </summary>
        /// <param name="collectionType">数组、<see cref="IEnumerable{T}"/> 或实现泛型 IEnumerable 的类型。</param>
        /// <returns>元素类型；<paramref name="collectionType"/> 为 <c>null</c> 或无法识别时返回 <c>null</c>。</returns>
        public static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType == null)
                return null;

            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return collectionType.GetGenericArguments()[0];

            var enumerableType = collectionType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerableType?.GetGenericArguments()[0];
        }

        /// <summary>
        /// 判断类型是否为 <see cref="ValueTuple"/> 元组。
        /// 1 至 8 元的 ValueTuple 是各自独立的泛型类型定义，且没有公共的非泛型标记接口可供判断，
        /// 因此这里按泛型定义的 FullName 前缀 <c>System.ValueTuple`</c> 统一识别。
        /// </summary>
        /// <param name="type">待判断类型。</param>
        /// <returns>属于 ValueTuple 泛型时返回 <c>true</c>；<paramref name="type"/> 为 <c>null</c> 或非泛型时返回 <c>false</c>。</returns>
        public static bool IsTupleType(Type type)
        {
            if (type == null || !type.IsGenericType)
                return false;

            var definition = type.GetGenericTypeDefinition();
            var fullName = definition.FullName;

            return fullName != null && fullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断类型是否为封闭且元素类型有效的多泛型条目值。
        /// </summary>
        /// <param name="type">待判断类型。</param>
        /// <returns>类型实现多值标记且所有泛型元素均为受支持的非嵌套条目值类型时返回 <c>true</c>。</returns>
        public static bool IsEntryMultipleValueType(Type type)
        {
            if (type == null)
                return false;
            var genericArguments = type.GetGenericArguments();
            return IsEntryMultipleValueType(type, genericArguments.Length);
        }

        /// <summary>
        /// 判断类型是否为具有指定元素数的封闭多泛型条目值。
        /// 多元素条目值以无外层定界符的平铺逗号格式编码，元素直接嵌套同类格式会产生无法逆解的分隔歧义，
        /// 因此泛型元素必须是非嵌套的受支持条目值类型。
        /// </summary>
        /// <param name="type">待判断类型。</param>
        /// <param name="expectedCount">期望的泛型元素数。</param>
        /// <returns>类型实现 <see cref="IEntryMultipleValue"/>、已封闭、元素数相符且全部元素合法时返回 <c>true</c>。</returns>
        public static bool IsEntryMultipleValueType(Type type, int expectedCount)
        {
            if (type == null || !type.IsGenericType || type.ContainsGenericParameters)
                return false;

            if (!typeof(IEntryMultipleValue).IsAssignableFrom(type))
                return false;

            var genericArguments = type.GetGenericArguments();
            if (genericArguments.Length != expectedCount)
                return false;

            foreach (var genericArgument in genericArguments)
            {
                if (IsEntryMultipleValueType(genericArgument) || !IsEntryValueType(genericArgument))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判断条目键名是否符合共享约束：非空且仅包含 Unicode 字母、数字或下划线。
        /// </summary>
        /// <param name="key">待检查的键名。</param>
        /// <returns>键名是否可以安全写入键值行。</returns>
        public static bool IsValidEntryKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && key.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        /// <summary>
        /// 判断表键名是否符合共享约束：非空且仅包含 Unicode 字母、数字或下划线。
        /// </summary>
        /// <param name="key">待检查的表名。</param>
        /// <returns>表名是否可以安全写入方括号表头。</returns>
        public static bool IsValidTableKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && key.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        /// <summary>
        /// 按共享条目模型的等值规则比较两个值。
        /// 两者的运行时类型必须相同，否则直接视为不相等。原始类型、字符串、枚举按 <see cref="object.Equals(object, object)"/> 比较；
        /// 数组与 <see cref="IEnumerable"/> 按元素顺序递归深度比较；<see cref="IEntryValue"/> 委托给其实现的业务等值判断。
        /// 其余类型一律视为不相等，使条目按“值已变化”保守处理。
        /// </summary>
        /// <param name="a">第一个待比较值，可为 <c>null</c>。</param>
        /// <param name="b">第二个待比较值，可为 <c>null</c>；仅两者同为 <c>null</c> 时相等。</param>
        /// <returns>类型相同且内容符合上述规则时返回 <c>true</c>。</returns>
        /// <remarks>
        /// 数组分支必须先于 <see cref="IEnumerable"/> 判断（数组本身实现了 <see cref="IEnumerable"/>），以便先比较秩和各维长度；
        /// <see cref="IEntryValue"/> 分支先于 <see cref="IEnumerable"/>，保证同时可枚举的自定义值类型使用其业务等值语义。
        /// 枚举序列会被完整遍历，且枚举器在可释放时会被释放；调用方不应传入无限序列或枚举有破坏性副作用的源。
        /// </remarks>
        public static bool ValueEqual(object a, object b)
        {
            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            var type = a.GetType();

            if (type != b.GetType())
                return false;

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                return object.Equals(a, b);

            if (type.IsArray)
            {
                if (!(a is Array arrayA) || !(b is Array arrayB))
                    return false;

                if (arrayA.Rank != arrayB.Rank || arrayA.Length != arrayB.Length)
                    return false;

                for (int dimension = 0; dimension < arrayA.Rank; dimension++)
                {
                    if (arrayA.GetLength(dimension) != arrayB.GetLength(dimension))
                        return false;
                }

                return EnumerableEqual(arrayA, arrayB);
            }

            if (typeof(IEntryValue).IsAssignableFrom(type))
            {
                if (!(a is IEntryValue valueA) || !(b is IEntryValue valueB))
                    return false;

                return valueA.Equals(valueB);
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return EnumerableEqual((IEnumerable)a, (IEnumerable)b);

            return false;
        }

        /// <summary>
        /// 按 <see cref="ValueEqual(object, object)"/> 的内容规则计算哈希值。
        /// </summary>
        /// <param name="value">待计算的值，可为 <c>null</c>。</param>
        /// <returns>与共享等值规则匹配的哈希值。</returns>
        public static int ValueHashCode(object value)
        {
            if (value == null)
                return 0;

            var type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                return value.GetHashCode();

            if (type.IsArray)
            {
                var array = (Array)value;
                unchecked
                {
                    var hash = array.Rank;
                    for (int dimension = 0; dimension < array.Rank; dimension++)
                        hash = (hash * 397) ^ array.GetLength(dimension);
                    return (hash * 397) ^ EnumerableHashCode(array);
                }
            }

            if (value is IEntryValue)
                return value.GetHashCode();

            if (value is IEnumerable enumerable)
                return EnumerableHashCode(enumerable);

            return type.GetHashCode();
        }

        private static bool EnumerableEqual(IEnumerable a, IEnumerable b)
        {
            var enumA = a?.GetEnumerator();
            var enumB = b?.GetEnumerator();

            if (enumA == null || enumB == null)
                return false;

            try
            {
                while (true)
                {
                    var hasNextA = enumA.MoveNext();
                    var hasNextB = enumB.MoveNext();

                    if (hasNextA != hasNextB)
                        return false;
                    if (!hasNextA)
                        return true;
                    if (!ValueEqual(enumA.Current, enumB.Current))
                        return false;
                }
            }
            finally
            {
                (enumA as IDisposable)?.Dispose();
                (enumB as IDisposable)?.Dispose();
            }
        }

        private static int EnumerableHashCode(IEnumerable value)
        {
            var enumerator = value?.GetEnumerator();
            if (enumerator == null)
                return 0;

            try
            {
                unchecked
                {
                    var hash = 17;
                    while (enumerator.MoveNext())
                        hash = (hash * 397) ^ ValueHashCode(enumerator.Current);
                    return hash;
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }
}
