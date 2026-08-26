using System;
using System.Linq;
using System.Reflection;
using UnityModBase.HConfigSpace;

namespace UnityModBase.HClassAttribute
{
    public static partial class ClassHelper
    {
        /// <summary>
        /// 在配置管理器的静态属性中定位暴露指定配置项的属性，并返回该属性上直接声明的全部特性，
        /// 供 <see cref="T:UnityModBase.HConfigGUI.UiMetadataHelper"/> 解析 GUI 元数据。
        /// </summary>
        /// <param name="classType">以静态属性暴露配置项的配置管理器类型。</param>
        /// <param name="entry">要在 <paramref name="classType"/> 中定位的运行时配置项。</param>
        /// <returns>
        /// 匹配属性上的全部特性，不按类型筛选，也不包含继承的特性；
        /// 找不到匹配属性时返回 <c>null</c>，匹配属性没有任何特性时返回空数组。
        /// </returns>
        /// <remarks>
        /// 定位依据是属性的当前值而非属性名：读取每个带静态 getter 的非索引属性，
        /// 把取值结果按“表键名 + 配置项键名”与 <paramref name="entry"/> 比较。
        /// 属性名因此可以与配置键不同，但要求静态属性在调用前已完成初始化，取值为 <c>null</c> 或非配置项的属性会被跳过。
        /// 多个属性暴露同一配置项时不提前退出，以遍历到的最后一个匹配属性为准。
        /// 本方法不做缓存，每次调用都会对所有静态属性取值；属性取值或特性读取抛出的异常直接向调用方传播。
        /// </remarks>
        public static Attribute[] GetEntryDeclarationAttributes(Type classType, IConfigEntry entry)
        {
            var properties = classType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            PropertyInfo targetProperty = null;
            foreach (var propertyInfo in properties)
            {
                if (propertyInfo.GetIndexParameters().Length > 0)
                    continue;

                var getter = propertyInfo.GetGetMethod(true);
                if (getter == null || !getter.IsStatic)
                    continue;

                var value = propertyInfo.GetValue(null) as IConfigEntry;
                if (value == null)
                    continue;

                if (value.TableKey == entry.TableKey && value.Key == entry.Key)
                    targetProperty = propertyInfo;
            }

            return targetProperty?.GetCustomAttributes(false).Cast<Attribute>().ToArray();
        }
    }
}
