using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModBase.HEntrySpace;

namespace UnityModBase.HConfigSpace
{
    public static partial class ConfigFileModel
    {
        /// <summary>
        /// 将集合文本拆分为元素文本。
        /// </summary>
        /// <param name="value">形如 <c>[a,b]</c> 的集合文本。</param>
        /// <returns>元素文本数组；引号未闭合、括号不平衡或存在空元素时返回失败。</returns>
        public static ConfigFileResult<string[]> SplitCollectionString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConfigFileResult<string[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Collection string cannot be empty"));
            else
                return SplitCompositeString(value, '[', ']');
        }

        /// <summary>
        /// 将集合编码为形如 <c>[a,b]</c> 的文本，各元素按元素类型递归编码。
        /// 元素文本内部可能包含引号、转义或嵌套括号，但顶层逗号一定是分隔符，与 <see cref="SplitCompositeString"/> 的词法规则互逆。
        /// </summary>
        /// <param name="type">集合声明类型，仅用于推导元素类型。</param>
        /// <param name="value">待编码集合，必须可枚举；空集合编码为 <c>[]</c>。</param>
        /// <returns>编码文本；元素类型无法识别或任一元素编码失败时返回失败。</returns>
        public static ConfigFileResult<string> EncodeCollection(Type type, object value)
        {
            if (type == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Type cannot be null"));
            if (value == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Value cannot be null"));

            var collectionType = EntryModel.GetCollectionElementType(type);
            if (collectionType == null)
                return ConfigFileResult<string>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Unsupported collection type"));

            var elements = new List<string>();
            foreach (var item in (IEnumerable)value)
            {
                var result = Encode(collectionType, item);
                if (!result.Success)
                    return ConfigFileResult<string>.Fail(result.Errors);
                elements.Add(result.Value);
            }

            // 每个元素先独立完成引号、转义或嵌套集合编码；解码器据此只把顶层逗号视为分隔符。
            return ConfigFileResult<string>.Ok($"[{string.Join(",", elements)}]");
        }

        /// <summary>
        /// 将形如 <c>[a,b]</c> 的集合文本解码为目标集合实例。
        /// 元素按推导出的元素类型逐个递归解码，最终由 <see cref="CreateCollectionResult"/> 按目标类型选择数组、列表、构造函数或 Add 路径完成实例化。
        /// </summary>
        /// <param name="type">目标集合类型，决定最终实例的创建方式。</param>
        /// <param name="value">被方括号包裹的集合文本。</param>
        /// <returns>目标集合实例；元素类型无法识别、拆分失败、任一元素解码失败或目标类型无可用创建路径时返回失败。</returns>
        public static ConfigFileResult<object> DecodeCollection(Type type, string value)
        {
            if (type == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Type cannot be null"));
            if (value == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Value cannot be null"));

            var collectionType = EntryModel.GetCollectionElementType(type);
            if (collectionType == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, "Unsupported collection type"));

            var splitResult = SplitCollectionString(value);
            if (!splitResult.Success)
                return ConfigFileResult<object>.Fail(splitResult.Errors);

            var elements = new List<object>();
            foreach (var item in splitResult.Value)
            {
                var result = Decode(collectionType, item.Trim());
                if (!result.Success)
                    return ConfigFileResult<object>.Fail(result.Errors);
                elements.Add(result.Value);
            }

            return CreateCollectionResult(type, collectionType, elements);
        }

        /// <summary>
        /// 根据目标集合类型创建解码结果。
        /// 创建顺序依次为数组、可直接赋值的 <see cref="List{T}"/>、接受辅助 List/数组的公开构造函数，
        /// 最后是公开无参构造函数加兼容的 <c>Add</c> 方法；所有路径都使用已经校验类型的元素。
        /// </summary>
        /// <param name="type">目标集合类型。</param>
        /// <param name="elementType">集合元素类型。</param>
        /// <param name="elements">已解码但尚未放入目标集合的元素。</param>
        /// <returns>目标集合实例。</returns>
        internal static ConfigFileResult<object> CreateCollectionResult(Type type, Type elementType, List<object> elements)
        {
            if (type == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Type cannot be null"));
            if (elementType == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Element type cannot be null"));
            if (elements == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Elements cannot be null"));

            var validatedElementsResult = ValidateCollectionElements(elementType, elements);
            if (!validatedElementsResult.Success)
                return ConfigFileResult<object>.Fail(validatedElementsResult.Errors);

            var arrayResult = CreateTypedArray(elementType, validatedElementsResult.Value);
            if (!arrayResult.Success)
                return ConfigFileResult<object>.Fail(arrayResult.Errors);

            if (type.IsArray)
                return ConfigFileResult<object>.Ok(arrayResult.Value);

            var listResult = CreateTypedList(elementType, validatedElementsResult.Value);
            if (!listResult.Success)
                return ConfigFileResult<object>.Fail(listResult.Errors);

            var list = listResult.Value;
            var listType = list.GetType();
            if (type.IsAssignableFrom(listType))
                return ConfigFileResult<object>.Ok(list);

            if (TryCreateCollectionFromConstructor(type, list, arrayResult.Value, out var constructorResult))
                return constructorResult;

            if (TryCreateCollectionFromAddMethod(type, elementType, validatedElementsResult.Value, out var addMethodResult))
                return addMethodResult;

            return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Unsupported collection type: {type.FullName}"));
        }

        /// <summary>
        /// 使用反射创建指定元素类型的数组。
        /// </summary>
        /// <param name="elementType">数组元素类型。</param>
        /// <param name="elements">数组元素。</param>
        /// <returns>创建出的数组实例。</returns>
        internal static ConfigFileResult<Array> CreateTypedArray(Type elementType, object[] elements)
        {
            try
            {
                if (elementType == null)
                    return ConfigFileResult<Array>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Element type cannot be null"));
                if (elements == null)
                    return ConfigFileResult<Array>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Elements cannot be null"));

                var array = Array.CreateInstance(elementType, elements.Length);
                for (int i = 0; i < elements.Length; i++)
                    array.SetValue(elements[i], i);

                return ConfigFileResult<Array>.Ok(array);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidCastException || ex is NotSupportedException)
            {
                return ConfigFileResult<Array>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to create array for element type {elementType.FullName}. Error: {ex.Message}"));
            }
        }

        /// <summary>
        /// 创建辅助 <see cref="List{T}"/>，用于后续直接返回或作为构造函数参数。
        /// </summary>
        /// <param name="elementType">列表元素类型。</param>
        /// <param name="elements">列表元素。</param>
        /// <returns>填充完成的泛型列表。</returns>
        internal static ConfigFileResult<IList> CreateTypedList(Type elementType, object[] elements)
        {
            try
            {
                if (elementType == null)
                    return ConfigFileResult<IList>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Element type cannot be null"));
                if (elements == null)
                    return ConfigFileResult<IList>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Elements cannot be null"));

                var listType = typeof(List<>).MakeGenericType(elementType);

                var list = (IList)Activator.CreateInstance(listType);
                for (int i = 0; i < elements.Length; i++)
                    list.Add(elements[i]);

                return ConfigFileResult<IList>.Ok(list);
            }
            catch (Exception ex) when (ex is MissingMethodException)
            {
                return ConfigFileResult<IList>.Fail(new ConfigFileError(ConfigFileErrorCode.UnsupportedType, $"Failed to create helper list for element type {elementType.FullName}. Error: {ex.Message}"));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidCastException || ex is NotSupportedException)
            {
                return ConfigFileResult<IList>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to populate helper list for element type {elementType.FullName}. Error: {ex.Message}"));
            }
        }

        /// <summary>
        /// 查找可接受指定元素类型的公开实例 <c>Add</c> 方法。
        /// 若存在多个兼容重载，返回反射枚举到的第一个方法，因此集合类型应避免提供语义不同但参数都兼容的重载。
        /// </summary>
        /// <param name="type">集合类型。</param>
        /// <param name="elementType">集合元素类型。</param>
        /// <returns>匹配的方法；不存在时返回 <c>null</c>。</returns>
        internal static MethodInfo FindAddMethod(Type type, Type elementType)
        {
            if (type == null || elementType == null)
                return null;

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == "Add")
                .ToArray();

            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                    continue;

                var parameterType = parameters[0].ParameterType;
                if (parameterType == elementType || parameterType.IsAssignableFrom(elementType))
                    return method;

                var underlyingType = Nullable.GetUnderlyingType(elementType);
                if (underlyingType != null && parameterType.IsAssignableFrom(underlyingType))
                    return method;
            }

            return null;
        }

        /// <summary>
        /// 尝试通过无参构造函数和 <c>Add</c> 方法创建目标集合类型。
        /// 创建或逐项添加失败时返回失败结果；此前已添加元素的临时实例不会暴露给调用方。
        /// </summary>
        /// <param name="type">目标集合类型。</param>
        /// <param name="elementType">集合元素类型。</param>
        /// <param name="elements">待添加元素。</param>
        /// <param name="result">如果找到了创建路径，则返回成功或失败结果；未找到时为 <c>null</c>。</param>
        /// <returns>是否找到了可执行的创建路径。</returns>
        internal static bool TryCreateCollectionFromAddMethod(Type type, Type elementType, object[] elements, out ConfigFileResult<object> result)
        {
            if (type == null || elementType == null || elements == null)
            {
                result = null;
                return false;
            }

            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                result = null;
                return false;
            }

            var addMethod = FindAddMethod(type, elementType);
            if (addMethod == null)
            {
                result = null;
                return false;
            }

            object instance;
            try
            {
                instance = constructor.Invoke(new object[0]);
            }
            catch (Exception ex) when (ex is TargetInvocationException || ex is MemberAccessException)
            {
                var message = ex is TargetInvocationException invocationException && invocationException.InnerException != null
                    ? invocationException.InnerException.Message
                    : ex.Message;
                result = ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to create collection type {type.FullName}. Error: {message}"));
                return true;
            }

            for (int i = 0; i < elements.Length; i++)
            {
                try
                {
                    addMethod.Invoke(instance, new[] { elements[i] });
                }
                catch (Exception ex) when (ex is TargetInvocationException || ex is ArgumentException || ex is TargetParameterCountException || ex is MethodAccessException)
                {
                    var message = ex is TargetInvocationException invocationException && invocationException.InnerException != null
                        ? invocationException.InnerException.Message
                        : ex.Message;
                    result = ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to add element at index {i} to collection type {type.FullName}. Error: {message}"));
                    return true;
                }
            }

            result = ConfigFileResult<object>.Ok(instance);
            return true;
        }

        /// <summary>
        /// 尝试通过单参数构造函数创建目标集合类型。
        /// 反射返回的首个兼容公开构造函数一旦执行（无论成功或抛出异常）即结束尝试，不会回退到其他兼容构造函数。
        /// </summary>
        /// <param name="type">目标集合类型。</param>
        /// <param name="list">辅助列表参数。</param>
        /// <param name="array">辅助数组参数。</param>
        /// <param name="result">如果找到了可用构造函数，则返回成功或失败结果；未找到时为 <c>null</c>。</param>
        /// <returns>是否找到了匹配的构造路径。</returns>
        internal static bool TryCreateCollectionFromConstructor(Type type, IList list, Array array, out ConfigFileResult<object> result)
        {
            if (type == null || list == null || array == null)
            {
                result = null;
                return false;
            }

            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .Where(c => c.GetParameters().Length == 1)
                .ToArray();

            var listType = list.GetType();
            var arrayType = array.GetType();

            for (int i = 0; i < constructors.Length; i++)
            {
                var constructor = constructors[i];
                var parameterType = constructor.GetParameters()[0].ParameterType;
                object argument = null;
                if (parameterType.IsAssignableFrom(listType))
                    argument = list;
                else if (parameterType.IsAssignableFrom(arrayType))
                    argument = array;
                else
                    continue;

                try
                {
                    result = ConfigFileResult<object>.Ok(constructor.Invoke(new[] { argument }));
                    return true;
                }
                catch (Exception ex) when (ex is TargetInvocationException || ex is ArgumentException || ex is MemberAccessException)
                {
                    var message = ex is TargetInvocationException invocationException && invocationException.InnerException != null
                        ? invocationException.InnerException.Message
                        : ex.Message;
                    result = ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Failed to construct collection type {type.FullName}. Error: {message}"));
                    return true;
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// 校验单个集合元素的赋值兼容性。
        /// </summary>
        /// <param name="elementType">集合声明的元素类型。</param>
        /// <param name="element">待校验元素。</param>
        /// <param name="index">元素在集合中的索引，用于错误定位。</param>
        /// <returns>可放入集合的元素值。</returns>
        internal static ConfigFileResult<object> ValidateCollectionElement(Type elementType, object element, int index)
        {
            if (elementType == null)
                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Element type cannot be null"));

            if (element == null)
            {
                var nullableUnderlyingType = Nullable.GetUnderlyingType(elementType);
                if (!elementType.IsValueType || nullableUnderlyingType != null)
                    return ConfigFileResult<object>.Ok(null);

                return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Element at index {index} cannot be null for value type {elementType.FullName}"));
            }

            if (elementType.IsInstanceOfType(element))
                return ConfigFileResult<object>.Ok(element);

            var underlyingType = Nullable.GetUnderlyingType(elementType);
            if (underlyingType != null && underlyingType.IsInstanceOfType(element))
                return ConfigFileResult<object>.Ok(element);

            return ConfigFileResult<object>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, $"Element at index {index} is not assignable to {elementType.FullName}. Actual type: {element.GetType().FullName}"));
        }

        /// <summary>
        /// 校验集合元素是否能安全放入目标元素类型。
        /// </summary>
        /// <param name="elementType">集合声明的元素类型。</param>
        /// <param name="elements">已解码元素。</param>
        /// <returns>校验后的元素数组。</returns>
        internal static ConfigFileResult<object[]> ValidateCollectionElements(Type elementType, List<object> elements)
        {
            if (elementType == null)
                return ConfigFileResult<object[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Element type cannot be null"));
            if (elements == null)
                return ConfigFileResult<object[]>.Fail(new ConfigFileError(ConfigFileErrorCode.InvalidValue, "Elements cannot be null"));

            var validatedElements = new object[elements.Count];
            for (int i = 0; i < elements.Count; i++)
            {
                var validationResult = ValidateCollectionElement(elementType, elements[i], i);
                if (!validationResult.Success)
                    return ConfigFileResult<object[]>.Fail(validationResult.Errors);

                validatedElements[i] = validationResult.Value;
            }

            return ConfigFileResult<object[]>.Ok(validatedElements);
        }
    }
}
