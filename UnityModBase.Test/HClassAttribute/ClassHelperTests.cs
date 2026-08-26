using System.Linq;
using Moq;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigSpace;

namespace UnityModBase.Test.HClassAttribute
{
    public class ClassHelperTests
    {
        // GetConfigGuiAttributes 按“静态运行时声明 + TableKey/Key 匹配”查找属性并返回其全部特性；
        // 静态属性的值在首次访问时创建，模拟生产环境中“运行时声明 + 文件项绑定”的查找方式。
        private class StaticConfigDeclarationHost
        {
            [ConfigSlider(0f, 100f, 1f)]
            public static IConfigEntry VolumeEntry { get; } = CreateConfigEntry("Settings", "Volume").Object;

            [ConfigSlider(10f, 50f, 0.5f)]
            public static IConfigEntry SpeedEntry { get; } = CreateConfigEntry("Movement", "Speed").Object;
        }

        private class InstanceConfigDeclarationHost
        {
            [ConfigSlider(0f, 100f, 1f)]
            public IConfigEntry VolumeEntry => CreateConfigEntry("Settings", "Volume").Object;
        }

        private class NonEntryStaticDeclarationHost
        {
            [ConfigSlider(0f, 1f, 0.1f)]
            public static float SliderValueWithoutEntry { get; set; }
        }

        private class MultiAttributeDeclarationHost
        {
            [ConfigSlider(0f, 10f, 1f)]
            [TestMarker]
            public static IConfigEntry VolumeEntry { get; } = CreateConfigEntry("Settings", "Volume").Object;
        }

        private class UnmarkedDeclarationHost
        {
            public static IConfigEntry VolumeEntry { get; } = CreateConfigEntry("Settings", "Volume").Object;
        }

        private class RenamedDeclarationHost
        {
            [ConfigSlider(0f, 100f, 1f)]
            public static IConfigEntry ExposedAsDifferentName { get; } = CreateConfigEntry("Settings", "ActualEntryKey").Object;
        }

        private class NullValueDeclarationHost
        {
            [ConfigSlider(0f, 100f, 1f)]
            public static IConfigEntry NullValueEntry => null;
        }

        private static Mock<IConfigEntry> CreateConfigEntry(string tableKey, string key)
        {
            var entryMock = new Mock<IConfigEntry>(MockBehavior.Strict);
            entryMock.SetupGet(x => x.TableKey).Returns(tableKey);
            entryMock.SetupGet(x => x.Key).Returns(key);
            return entryMock;
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenEntryMatchesStaticDeclaration_ReturnsItsSliderAttribute()
        {
            // Arrange
            var entryMock = CreateConfigEntry("Settings", "Volume");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(StaticConfigDeclarationHost), entryMock.Object);

            // Assert
            var slider = Assert.IsType<ConfigSliderAttribute>(Assert.Single(result));
            Assert.Equal(0f, slider.Min);
            Assert.Equal(100f, slider.Max);
            Assert.Equal(1f, slider.Step);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenEntryMatchesDifferentStaticDeclaration_ReturnsItsOwnValues()
        {
            // Arrange
            var entryMock = CreateConfigEntry("Movement", "Speed");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(StaticConfigDeclarationHost), entryMock.Object);

            // Assert
            var slider = Assert.IsType<ConfigSliderAttribute>(Assert.Single(result));
            Assert.Equal(10f, slider.Min);
            Assert.Equal(50f, slider.Max);
            Assert.Equal(0.5f, slider.Step);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenEntryTableKeyDoesNotMatchAnyDeclaration_ReturnsNull()
        {
            // Arrange：表键不一致时短路比较，不应读取配置键。
            var entryMock = CreateConfigEntry("OtherTable", "Volume");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(StaticConfigDeclarationHost), entryMock.Object);

            // Assert
            Assert.Null(result);
            entryMock.VerifyGet(x => x.Key, Times.Never);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenEntryKeyDoesNotMatchAnyDeclaration_ReturnsNull()
        {
            // Arrange：表键一致但配置键不同，说明匹配确实进行过却未命中。
            var entryMock = CreateConfigEntry("Settings", "OtherKey");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(StaticConfigDeclarationHost), entryMock.Object);

            // Assert
            Assert.Null(result);
            entryMock.VerifyGet(x => x.Key, Times.Once);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenMarkedDeclarationIsInstanceProperty_ReturnsNull()
        {
            // Arrange：配置特性标记在实例属性上，运行时声明扫描只覆盖静态属性。
            var entryMock = CreateConfigEntry("Settings", "Volume");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(InstanceConfigDeclarationHost), entryMock.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenStaticDeclarationValueIsNotConfigEntry_ReturnsNull()
        {
            // Arrange：标记了滑条特性的静态属性不是 IConfigEntry，应被跳过而不是按属性名匹配。
            var entryMock = CreateConfigEntry("Settings", "Volume");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(NonEntryStaticDeclarationHost), entryMock.Object);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenMatchedDeclarationHasMultipleAttributes_ReturnsAllOfThem()
        {
            // Arrange：返回值是匹配属性上的全部特性而非仅第一个；反射不保证特性间顺序，按类型断言。
            var entryMock = CreateConfigEntry("Settings", "Volume");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(MultiAttributeDeclarationHost), entryMock.Object);

            // Assert
            Assert.Equal(2, result.Length);
            Assert.Contains(result, attribute => attribute is ConfigSliderAttribute);
            Assert.Contains(result, attribute => attribute is TestMarkerAttribute);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenMatchedDeclarationHasNoAttributes_ReturnsEmptyArray()
        {
            // Arrange：命中属性但未标记任何特性时返回空数组，与“未命中返回 null”区分。
            var entryMock = CreateConfigEntry("Settings", "Volume");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(UnmarkedDeclarationHost), entryMock.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenPropertyNameDiffersFromEntryKey_MatchesByValue()
        {
            // Arrange：定位依据是静态属性的当前值（TableKey + Key）而非属性名，属性名与配置键不同也应命中。
            var entryMock = CreateConfigEntry("Settings", "ActualEntryKey");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(RenamedDeclarationHost), entryMock.Object);

            // Assert
            var slider = Assert.IsType<ConfigSliderAttribute>(Assert.Single(result));
            Assert.Equal(0f, slider.Min);
            Assert.Equal(100f, slider.Max);
        }

        [Fact]
        public void GetConfigGuiAttributes_WhenStaticDeclarationValueIsNull_SkipsProperty()
        {
            // Arrange：静态属性取值为 null 时无法参与匹配，应被跳过而不是按属性名或抛出空引用。
            var entryMock = CreateConfigEntry("Settings", "NullValueEntry");

            // Act
            var result = ClassHelper.GetEntryDeclarationAttributes(typeof(NullValueDeclarationHost), entryMock.Object);

            // Assert
            Assert.Null(result);
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
        private sealed class TestMarkerAttribute : Attribute
        {
        }

        private class PropertyAttributeContainer
        {
            [TestMarker]
            public static string MarkedPublicStaticProperty { get; private set; }

            [TestMarker]
            private static string MarkedPrivateStaticProperty { get; set; }

            [TestMarker]
            public string MarkedInstanceProperty { get; set; }

            public static string UnmarkedStaticProperty { get; set; }
        }

        [Fact]
        public void GetProperties_WhenClassHasMarkedStaticProperties_ReturnsOnlyMarkedStatics()
        {
            // Arrange：静态约定是扫描契约，实例属性即使带特性也不参与配置声明。
            var classType = typeof(PropertyAttributeContainer);

            // Act
            var result = ClassHelper.GetProperties<TestMarkerAttribute>(classType);

            // Assert
            Assert.Contains(result, property => property.Name == nameof(PropertyAttributeContainer.MarkedPublicStaticProperty));
            Assert.Contains(result, property => property.Name == "MarkedPrivateStaticProperty");
            Assert.DoesNotContain(result, property => property.Name == nameof(PropertyAttributeContainer.MarkedInstanceProperty));
            Assert.DoesNotContain(result, property => property.Name == nameof(PropertyAttributeContainer.UnmarkedStaticProperty));
        }

        [Fact]
        public void GetProperties_WhenClassHasNoMarkedProperties_ReturnsEmptyArray()
        {
            // Arrange
            var classType = typeof(MarkedClassA);

            // Act
            var result = ClassHelper.GetProperties<TestMarkerAttribute>(classType);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetProperties_WhenClassTypeIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                ClassHelper.GetProperties<TestMarkerAttribute>(null));
            Assert.Equal("classType", exception.ParamName);
        }

        [TestMarker]
        private class MarkedClassA
        {
        }

        [TestMarker]
        private class MarkedClassB
        {
        }

        private class UnmarkedClass
        {
        }

        [RegisterOnGameBoot]
        private class RegisterOnGameBootMarkedClass
        {
        }

        private class MethodAttributeContainer
        {
            [TestMarker]
            public void MarkedPublicInstanceMethod()
            {
            }

            [TestMarker]
            private void MarkedPrivateInstanceMethod()
            {
            }

            [TestMarker]
            public static void MarkedPublicStaticMethod()
            {
            }

            [TestMarker]
            private static void MarkedPrivateStaticMethod()
            {
            }

            public void UnmarkedMethod()
            {
            }
        }

        [Fact]
        public void GetClasses_WhenAssemblyContainsMarkedClasses_ReturnsOnlyMarkedClasses()
        {
            // Arrange
            var assembly = typeof(ClassHelperTests).Assembly;

            // Act
            var result = ClassHelper.GetClasses<TestMarkerAttribute>(assembly);

            // Assert
            Assert.Contains(typeof(MarkedClassA), result);
            Assert.Contains(typeof(MarkedClassB), result);
            Assert.DoesNotContain(typeof(UnmarkedClass), result);
        }

        [Fact]
        public void GetClasses_WhenAssemblyHasNoClassesWithAttribute_ReturnsEmptyArray()
        {
            // Arrange
            var assembly = typeof(string).Assembly;

            // Act
            var result = ClassHelper.GetClasses<RegisterOnGameBootAttribute>(assembly);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetMethods_WhenAssemblyContainsMarkedMethods_ReturnsMethodsWithAllSupportedBindingFlags()
        {
            // Arrange
            var assembly = typeof(ClassHelperTests).Assembly;

            // Act
            var result = ClassHelper.GetMethods<TestMarkerAttribute>(assembly);

            // Assert
            Assert.Contains(result, method => method.DeclaringType == typeof(MethodAttributeContainer) && method.Name == nameof(MethodAttributeContainer.MarkedPublicInstanceMethod));
            Assert.Contains(result, method => method.DeclaringType == typeof(MethodAttributeContainer) && method.Name == "MarkedPrivateInstanceMethod");
            Assert.Contains(result, method => method.DeclaringType == typeof(MethodAttributeContainer) && method.Name == nameof(MethodAttributeContainer.MarkedPublicStaticMethod));
            Assert.Contains(result, method => method.DeclaringType == typeof(MethodAttributeContainer) && method.Name == "MarkedPrivateStaticMethod");
            Assert.DoesNotContain(result, method => method.DeclaringType == typeof(MethodAttributeContainer) && method.Name == nameof(MethodAttributeContainer.UnmarkedMethod));
        }

        [Fact]
        public void GetMethods_WhenAssemblyHasNoMethodsWithAttribute_ReturnsEmptyArray()
        {
            // Arrange
            var assembly = typeof(string).Assembly;

            // Act
            var result = ClassHelper.GetMethods<RegisterOnGameBootAttribute>(assembly);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetClasses_WhenAssemblyIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                ClassHelper.GetClasses<TestMarkerAttribute>(null));
            Assert.Equal("assembly", exception.ParamName);
        }

        [Fact]
        public void GetMethods_WhenAssemblyIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                ClassHelper.GetMethods<TestMarkerAttribute>(null));
            Assert.Equal("assembly", exception.ParamName);
        }

        [Fact]
        public void GetRegisterOnGameBootClasses_WhenAssemblyContainsMarkedClasses_ReturnsMarkedClasses()
        {
            // Arrange
            var assembly = typeof(ClassHelperTests).Assembly;

            // Act
            var result = ClassHelper.GetRegisterOnGameBootClasses(assembly);

            // Assert
            Assert.Contains(result, type => type == typeof(RegisterOnGameBootMarkedClass));
            Assert.DoesNotContain(result, type => type == typeof(UnmarkedClass));
        }

        private class InitializeOnGameBootMethodContainer
        {
            [InitializeOnGameBoot]
            public void MarkedPublicInstanceMethod()
            {
            }

            [InitializeOnGameBoot]
            private static void MarkedPrivateStaticMethod()
            {
            }

            public void UnmarkedMethod()
            {
            }
        }

        [Fact]
        public void GetInitializeOnGameBootMethods_WhenAssemblyContainsMarkedMethods_ReturnsOnlyMarkedMethods()
        {
            // Arrange
            var assembly = typeof(ClassHelperTests).Assembly;

            // Act
            var result = ClassHelper.GetInitializeOnGameBootMethods(assembly);

            // Assert
            Assert.Contains(result, method => method.DeclaringType == typeof(InitializeOnGameBootMethodContainer) && method.Name == nameof(InitializeOnGameBootMethodContainer.MarkedPublicInstanceMethod));
            Assert.Contains(result, method => method.DeclaringType == typeof(InitializeOnGameBootMethodContainer) && method.Name == "MarkedPrivateStaticMethod");
            Assert.DoesNotContain(result, method => method.DeclaringType == typeof(InitializeOnGameBootMethodContainer) && method.Name == nameof(InitializeOnGameBootMethodContainer.UnmarkedMethod));
        }
        [Fact]
        public void GetInitializeOnGameBootMethods_WhenAssemblyHasNoMarkedMethods_ReturnsEmptyArray()
        {
            // Arrange
            var assembly = typeof(string).Assembly;

            // Act
            var result = ClassHelper.GetInitializeOnGameBootMethods(assembly);

            // Assert
            Assert.Empty(result);
        }
    }
}
