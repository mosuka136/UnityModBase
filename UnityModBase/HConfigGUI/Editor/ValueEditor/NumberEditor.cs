using System;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HProvider;

namespace UnityModBase.HConfigGUI.Editor.ValueEditor
{
    public class NumberEditor : IValueEditor
    {
        public float DelayApplyDuration { get; set; } = 0.5f;

        public IUnityGuiProvider UnityGui { get; }

        public NumberEditor(IUnityGuiProvider unityGui)
        {
            UnityGui = unityGui;
        }

        public virtual bool CanEdit(IEntryBinding entry)
        {
            var type = entry.ValueType;
            return type.IsPrimitive && type != typeof(bool) && type != typeof(char);
        }

        public virtual void DrawValue(IEntryBinding entry, GuiStateStore state, EntryChangeSink changeSink)
        {
            if (!entry.ValueType.IsPrimitive || entry.ValueType == typeof(bool) || entry.ValueType == typeof(char))
                return;

            var key = state.GetKey(entry, "_number");
            var valueString = state.GetText(key, entry.Value.ToString());
            string newValueString = UnityGui.TextField(valueString, UnityGui.ExpandWidth(true));
            if (valueString != newValueString)
            {
                state.SetText(key, newValueString);
                SetValue(entry, newValueString, changeSink);
            }
        }

        public void SetValue(IEntryBinding entry, string newValueString, EntryChangeSink changeSink)
        {
            object parsedValue;
            bool isValid;

            switch (entry.ValueType)
            {
                case Type t when t == typeof(byte):
                    isValid = byte.TryParse(newValueString, out var byteValue);
                    parsedValue = byteValue;
                    break;
                case Type t when t == typeof(sbyte):
                    isValid = sbyte.TryParse(newValueString, out var sbyteValue);
                    parsedValue = sbyteValue;
                    break;
                case Type t when t == typeof(short):
                    isValid = short.TryParse(newValueString, out var shortValue);
                    parsedValue = shortValue;
                    break;
                case Type t when t == typeof(ushort):
                    isValid = ushort.TryParse(newValueString, out var ushortValue);
                    parsedValue = ushortValue;
                    break;
                case Type t when t == typeof(int):
                    isValid = int.TryParse(newValueString, out var intValue);
                    parsedValue = intValue;
                    break;
                case Type t when t == typeof(uint):
                    isValid = uint.TryParse(newValueString, out var uintValue);
                    parsedValue = uintValue;
                    break;
                case Type t when t == typeof(long):
                    isValid = long.TryParse(newValueString, out var longValue);
                    parsedValue = longValue;
                    break;
                case Type t when t == typeof(ulong):
                    isValid = ulong.TryParse(newValueString, out var ulongValue);
                    parsedValue = ulongValue;
                    break;
                case Type t when t == typeof(float):
                    isValid = float.TryParse(newValueString, out var floatValue);
                    parsedValue = floatValue;
                    break;
                case Type t when t == typeof(double):
                    isValid = double.TryParse(newValueString, out var doubleValue);
                    parsedValue = doubleValue;
                    break;
                default:
                    return;
            }

            changeSink.SetValue(entry, isValid ? parsedValue : newValueString, isValid, DelayApplyDuration);
        }

        public void DrawExtra(IEntryBinding entry, GuiStateStore state, EntryChangeSink changeSink)
        {
        }
    }
}
