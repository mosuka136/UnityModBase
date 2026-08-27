using UnityModBase.HConfigGUI;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HGuiSpace;
using UnityModBase.HGuiSpace.Bindings;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using Moq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityModBase.Test.HConfigGUI.Editor
{
    /// <summary>
    /// 验证热键编辑状态机及有效开关的恢复语义。
    /// <see cref="Hotkey.GlobalValid"/> 是跨实例共享状态，因此每个测试前后都重置该开关以隔离用例。
    /// </summary>
    public class HotkeyEditSessionTests : IDisposable
    {
        /// <summary>
        /// 在当前测试建立会话前恢复全局热键的默认有效状态。
        /// </summary>
        public HotkeyEditSessionTests()
        {
            Hotkey.GlobalValid = true;
        }

        [Theory]
        [InlineData(false, HotkeyEditState.Expanded)]
        [InlineData(true, HotkeyEditState.Idle)]
        public void IsEditing_WhenEntryIsMissingOrStateIsIdle_ReturnsFalse(bool hasEntry, HotkeyEditState state)
        {
            var session = new HotkeyEditSession
            {
                Entry = hasEntry ? CreateEntryBinding(new Hotkey()).Object : null,
                State = state
            };

            var result = session.IsEditing;

            Assert.False(result);
        }

        [Fact]
        public void IsEditing_WhenEntryExistsAndStateIsNotIdle_ReturnsTrue()
        {
            var session = new HotkeyEditSession
            {
                Entry = CreateEntryBinding(new Hotkey()).Object,
                State = HotkeyEditState.Expanded
            };

            var result = session.IsEditing;

            Assert.True(result);
        }

        [Theory]
        [InlineData(HotkeyEditState.Idle)]
        [InlineData(HotkeyEditState.Expanded)]
        public void IsRecording_WhenStateIsIdleOrExpanded_ReturnsFalse(HotkeyEditState state)
        {
            var session = new HotkeyEditSession
            {
                State = state
            };

            var result = session.IsRecording;

            Assert.False(result);
        }

        [Theory]
        [InlineData(HotkeyEditState.WaitingPress)]
        [InlineData(HotkeyEditState.Recording)]
        [InlineData(HotkeyEditState.WaitingConfirm)]
        public void IsRecording_WhenStateRequiresInput_ReturnsTrue(HotkeyEditState state)
        {
            var session = new HotkeyEditSession
            {
                State = state
            };

            var result = session.IsRecording;

            Assert.True(result);
        }

        [Theory]
        [InlineData(HotkeyEditState.Idle)]
        [InlineData(HotkeyEditState.Expanded)]
        public void Update_WhenStateIsIdleOrExpanded_ReturnsWithoutChangingState(HotkeyEditState state)
        {
            var session = new HotkeyEditSession
            {
                State = state
            };

            session.Update();

            Assert.Equal(state, session.State);
        }

        [Fact]
        public void Update_WhenStateIsWaitingPressWithoutInput_LeavesStateUnchanged()
        {
            var unityProvider = UnityProvider.Instance;
            var session = new HotkeyEditSession
            {
                Entry = CreateEntryBinding(new Hotkey()).Object,
                State = HotkeyEditState.WaitingPress,
                WorkingValue = new Hotkey()
            };

            session.Update();

            Assert.Equal(HotkeyEditState.WaitingPress, session.State);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }

        [Fact]
        public void Update_WhenStateIsRecordingWithoutInput_MovesToWaitingConfirm()
        {
            var unityProvider = UnityProvider.Instance;
            var session = new HotkeyEditSession
            {
                Entry = CreateEntryBinding(new Hotkey()).Object,
                State = HotkeyEditState.Recording,
                WorkingValue = new Hotkey()
            };

            session.Update();

            Assert.Equal(HotkeyEditState.WaitingConfirm, session.State);
        }

        [Fact]
        public void Update_WhenStateIsWaitingConfirmAndWorkingChordIsInvalid_ClearsPreviewsAndMovesToRecording()
        {
            var unityProvider = UnityProvider.Instance;
            var session = new HotkeyEditSession
            {
                Entry = CreateEntryBinding(new Hotkey()).Object,
                State = HotkeyEditState.WaitingConfirm,
                WorkingValue = new Hotkey(),
                WorkingChord = new HotkeyChord(unityProvider),
                PreviewGamepadChord = new GamepadChord(unityProvider, new GamepadTrigger(GamepadButton.DpadUp, unityProvider)),
                PreviewKeyboardChord = new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider))
            };

            session.Update();

            Assert.Equal(HotkeyEditState.Recording, session.State);
            Assert.False(session.PreviewGamepadChord.IsValid);
            Assert.False(session.PreviewKeyboardChord.IsValid);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Clear_WhenRecording_RestoresCapturedValidityAndResetsSessionState(
            bool globalValidBeforeEdit,
            bool originalValidBeforeEdit)
        {
            var unityProvider = UnityProvider.Instance;
            var hotkey = new Hotkey()
            {
                Valid = originalValidBeforeEdit
            };
            var entry = CreateEntryBinding(hotkey);
            var session = new HotkeyEditSession();
            Hotkey.GlobalValid = globalValidBeforeEdit;

            session.BeginEdit(entry.Object);
            session.BeginRecord(new HotkeyChord(unityProvider));
            session.PreviewGamepadChord = new GamepadChord(
                unityProvider,
                new GamepadTrigger(GamepadButton.DpadUp, unityProvider));
            session.PreviewKeyboardChord = new KeyboardChord(
                unityProvider,
                new KeyboardTrigger(Key.A, unityProvider));

            Assert.False(hotkey.Valid);
            Assert.False(Hotkey.GlobalValid);

            session.Clear();

            Assert.Equal(originalValidBeforeEdit, hotkey.Valid);
            Assert.Equal(globalValidBeforeEdit, Hotkey.GlobalValid);
            Assert.Null(session.Entry);
            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.OriginalValue);
            Assert.Null(session.WorkingValue);
            Assert.Null(session.WorkingChord);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }


        [Fact]
        public void BeginEdit_WhenEntryContainsHotkey_InitializesEditingState()
        {
            var unityProvider = UnityProvider.Instance;
            var hotkey = new Hotkey();
            hotkey.Add(new HotkeyChord(unityProvider));
            var entry = CreateEntryBinding(hotkey);
            var session = new HotkeyEditSession
            {
                WorkingChord = new HotkeyChord(unityProvider),
                PreviewGamepadChord = new GamepadChord(unityProvider, new GamepadTrigger(GamepadButton.DpadUp, unityProvider)),
                PreviewKeyboardChord = new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider))
            };

            session.BeginEdit(entry.Object);

            Assert.Same(entry.Object, session.Entry);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
            Assert.Same(hotkey, session.OriginalValue);
            Assert.NotSame(hotkey, session.WorkingValue);
            Assert.Equal(hotkey.Count, session.WorkingValue.Count);
            Assert.False(session.WorkingValue.Valid);
            Assert.Null(session.WorkingChord);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }


        [Fact]
        public void BeginEdit_WhenEntryIsNull_DoesNothing()
        {
            var session = new HotkeyEditSession
            {
                State = HotkeyEditState.Idle
            };

            session.BeginEdit(null);

            Assert.Null(session.Entry);
            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.OriginalValue);
            Assert.Null(session.WorkingValue);
        }

        [Fact]
        public void BeginEdit_WhenSessionIsNotIdle_DoesNothing()
        {
            var unityProvider = UnityProvider.Instance;
            var hotkey = new Hotkey();
            var entry = CreateEntryBinding(hotkey);
            var session = new HotkeyEditSession
            {
                State = HotkeyEditState.Expanded
            };

            session.BeginEdit(entry.Object);

            Assert.Null(session.Entry);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
            Assert.Null(session.OriginalValue);
            Assert.Null(session.WorkingValue);
        }

        [Fact]
        public void BeginRecord_WhenSessionIsNotExpanded_DoesNothing()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey()
            {
                Valid = true
            };
            var workingChord = new HotkeyChord(unityProvider);
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession
            {
                Entry = entry.Object,
                State = HotkeyEditState.WaitingPress,
                OriginalValue = originalValue
            };
            Hotkey.GlobalValid = true;

            session.BeginRecord(workingChord);

            Assert.True(Hotkey.GlobalValid);
            Assert.True(originalValue.Valid);
            Assert.Null(session.WorkingChord);
            Assert.Equal(HotkeyEditState.WaitingPress, session.State);
        }

        [Fact]
        public void BeginRecord_WhenSessionIsExpanded_SetsRecordingState()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey()
            {
                Valid = true
            };
            var workingChord = new HotkeyChord(unityProvider);
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession
            {
                Entry = entry.Object,
                State = HotkeyEditState.Expanded,
                OriginalValue = originalValue
            };
            Hotkey.GlobalValid = true;

            session.BeginRecord(workingChord);

            Assert.False(Hotkey.GlobalValid);
            Assert.False(originalValue.Valid);
            Assert.Same(workingChord, session.WorkingChord);
            Assert.Equal(HotkeyEditState.WaitingPress, session.State);
        }

        [Fact]
        public void CancelRecord_WhenSessionIsExpanded_DoesNothing()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey()
            {
                Valid = false
            };
            var previousWorkingValue = new Hotkey();
            var workingChord = new HotkeyChord(unityProvider);
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession
            {
                Entry = entry.Object,
                State = HotkeyEditState.Expanded,
                OriginalValue = originalValue,
                WorkingValue = previousWorkingValue,
                WorkingChord = workingChord
            };
            Hotkey.GlobalValid = false;

            session.CancelRecord();

            Assert.False(Hotkey.GlobalValid);
            Assert.False(originalValue.Valid);
            Assert.Same(previousWorkingValue, session.WorkingValue);
            Assert.Same(workingChord, session.WorkingChord);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void CancelRecord_WhenRecording_RestoresCapturedValidityAndEditingState()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey()
            {
                Valid = false
            };
            originalValue.Add(new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)), unityProvider));
            var workingChord = new HotkeyChord(unityProvider);
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession();
            Hotkey.GlobalValid = false;

            session.BeginEdit(entry.Object);
            var workingValueBeforeRecord = session.WorkingValue;
            session.BeginRecord(workingChord);
            session.CancelRecord();

            Assert.False(Hotkey.GlobalValid);
            Assert.False(originalValue.Valid);
            Assert.NotSame(workingValueBeforeRecord, session.WorkingValue);
            Assert.NotSame(originalValue, session.WorkingValue);
            Assert.Equal(originalValue.Count, session.WorkingValue.Count);
            Assert.False(session.WorkingValue.Valid);
            Assert.Null(session.WorkingChord);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void CancelEdit_WhenRecording_RestoresCapturedValidityAndClearsSession()
        {
            var unityProvider = UnityProvider.Instance;
            var hotkey = new Hotkey()
            {
                Valid = false
            };
            var entry = CreateEntryBinding(hotkey);
            Hotkey.GlobalValid = false;
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.BeginRecord(new HotkeyChord(unityProvider));

            session.CancelEdit();

            Assert.False(hotkey.Valid);
            Assert.False(Hotkey.GlobalValid);
            Assert.Null(session.Entry);
            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.OriginalValue);
            Assert.Null(session.WorkingValue);
            Assert.Null(session.WorkingChord);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }

        [Fact]
        public void Dispose_WhenRecording_RestoresCapturedValidityAndClearsSession()
        {
            var unityProvider = UnityProvider.Instance;
            var hotkey = new Hotkey()
            {
                Valid = false
            };
            var entry = CreateEntryBinding(hotkey);
            Hotkey.GlobalValid = false;
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.BeginRecord(new HotkeyChord(unityProvider));

            session.Dispose();

            Assert.False(hotkey.Valid);
            Assert.False(Hotkey.GlobalValid);
            Assert.Null(session.Entry);
            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.OriginalValue);
            Assert.Null(session.WorkingValue);
            Assert.Null(session.WorkingChord);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }

        [Fact]
        public void ConfirmEdit_WhenSessionIsExpanded_PreservesValidityAndClearsSession()
        {
            var unityProvider = UnityProvider.Instance;
            var hotkey = new Hotkey()
            {
                Valid = false
            };
            var entry = CreateEntryBinding(hotkey);
            Hotkey.GlobalValid = false;
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);

            session.ConfirmEdit(new EntryChangeSink());

            Assert.Same(hotkey, entry.Object.Value);
            Assert.False(hotkey.Valid);
            Assert.False(Hotkey.GlobalValid);
            Assert.Null(session.Entry);
            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.OriginalValue);
            Assert.Null(session.WorkingValue);
            Assert.Null(session.WorkingChord);
        }

        [Fact]
        public void ConfirmEdit_WhenStateIsWaitingConfirm_ConfirmsChangeRestoresValidityAndClearsSession()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey()
            {
                Valid = false
            };
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession();
            var workingChord = new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)),
                unityProvider);
            Hotkey.GlobalValid = false;

            session.BeginEdit(entry.Object);
            session.BeginRecord(workingChord);
            session.State = HotkeyEditState.WaitingConfirm;
            session.ConfirmEdit(new EntryChangeSink());

            var updatedValue = Assert.IsType<Hotkey>(entry.Object.Value);
            Assert.NotSame(originalValue, updatedValue);
            Assert.Equal(1, updatedValue.Count);
            Assert.True(updatedValue.Valid);
            Assert.False(originalValue.Valid);
            Assert.False(Hotkey.GlobalValid);
            Assert.Null(session.Entry);
            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.OriginalValue);
            Assert.Null(session.WorkingValue);
            Assert.Null(session.WorkingChord);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }


        [Fact]
        public void ConfirmRecord_WhenGamepadPreviewIsValid_UpdatesEntryAndResetsEditingState()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            var entry = CreateEntryBinding(originalValue);
            var workingChord = new HotkeyChord(unityProvider);
            var previewGamepadChord = new GamepadChord(unityProvider, new GamepadTrigger(GamepadButton.DpadUp, unityProvider));
            var session = new HotkeyEditSession();
            Hotkey.GlobalValid = true;

            session.BeginEdit(entry.Object);
            session.BeginRecord(workingChord);
            session.State = HotkeyEditState.WaitingConfirm;
            session.PreviewGamepadChord = previewGamepadChord;
            session.PreviewKeyboardChord = new KeyboardChord(unityProvider);

            session.ConfirmRecord(new EntryChangeSink());

            var updatedValue = Assert.IsType<Hotkey>(entry.Object.Value);
            var updatedChord = Assert.Single(updatedValue.Hotkeys);
            Assert.Same(previewGamepadChord, updatedChord.Chord);
            Assert.True(updatedValue.Valid);
            Assert.True(Hotkey.GlobalValid);
            Assert.True(session.OriginalValue.Valid);
            Assert.Null(session.WorkingChord);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
            Assert.NotSame(updatedValue, session.WorkingValue);
            Assert.False(session.WorkingValue.Valid);
            Assert.Equal(updatedValue.Count, session.WorkingValue.Count);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void ConfirmRecord_WhenSessionIsNotWaitingConfirm_CancelsRecord()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey()
            {
                Valid = false
            };
            originalValue.Add(new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)), unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession();
            Hotkey.GlobalValid = false;

            session.BeginEdit(entry.Object);
            var previousWorkingValue = session.WorkingValue;
            session.BeginRecord(new HotkeyChord(unityProvider));
            session.ConfirmRecord(new EntryChangeSink());

            Assert.False(Hotkey.GlobalValid);
            Assert.False(originalValue.Valid);
            Assert.NotSame(previousWorkingValue, session.WorkingValue);
            Assert.Equal(originalValue.Count, session.WorkingValue.Count);
            Assert.False(session.WorkingValue.Valid);
            Assert.Null(session.WorkingChord);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void ConfirmRecord_WhenKeyboardPreviewIsValid_UsesKeyboardChord()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey()
            {
                Valid = false
            };
            var entry = CreateEntryBinding(originalValue);
            var workingChord = new HotkeyChord(unityProvider);
            var previewKeyboardChord = new KeyboardChord(unityProvider, new KeyboardTrigger(Key.B, unityProvider));
            var session = new HotkeyEditSession();
            Hotkey.GlobalValid = false;

            session.BeginEdit(entry.Object);
            session.BeginRecord(workingChord);
            session.State = HotkeyEditState.WaitingConfirm;
            session.PreviewGamepadChord = new GamepadChord(unityProvider);
            session.PreviewKeyboardChord = previewKeyboardChord;

            session.ConfirmRecord(new EntryChangeSink());

            var updatedValue = Assert.IsType<Hotkey>(entry.Object.Value);
            var updatedChord = Assert.Single(updatedValue.Hotkeys);
            Assert.Same(previewKeyboardChord, updatedChord.Chord);
            Assert.False(originalValue.Valid);
            Assert.False(Hotkey.GlobalValid);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void ConfirmRecord_WhenBothPreviewsAreValid_KeepsExistingWorkingChordAndWritesResult()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            var originalChord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.C, unityProvider)), unityProvider);
            originalValue.Add(originalChord);
            var entry = CreateEntryBinding(originalValue);
            var workingChord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.C, unityProvider)), unityProvider);
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.BeginRecord(workingChord);
            session.State = HotkeyEditState.WaitingConfirm;
            session.PreviewGamepadChord = new GamepadChord(
                unityProvider,
                new GamepadTrigger(GamepadButton.DpadUp, unityProvider));
            session.PreviewKeyboardChord = new KeyboardChord(
                unityProvider,
                new KeyboardTrigger(Key.V, unityProvider));

            session.ConfirmRecord(new EntryChangeSink());

            var updatedValue = Assert.IsType<Hotkey>(entry.Object.Value);
            Assert.Equal(2, updatedValue.Count);
            Assert.Equal("C,C", updatedValue.ToString());
            Assert.Equal("C", workingChord.ToString());
            Assert.Equal(HotkeyEditState.Expanded, session.State);
            Assert.Null(session.WorkingChord);
        }

        [Fact]
        public void ConfirmRecord_WhenResultHasNoValidChord_DoesNotWriteBackAndRefreshesWorkingCopy()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            originalValue.Add(new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.D, unityProvider)), unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.BeginRecord(new HotkeyChord(unityProvider));
            session.WorkingValue = new Hotkey();
            session.State = HotkeyEditState.WaitingConfirm;
            session.PreviewGamepadChord = new GamepadChord(unityProvider);
            session.PreviewKeyboardChord = new KeyboardChord(unityProvider);

            session.ConfirmRecord(new EntryChangeSink());

            Assert.Same(originalValue, entry.Object.Value);
            Assert.Equal(originalValue.Count, session.WorkingValue.Count);
            Assert.False(session.WorkingValue.Valid);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void SetWorkingChord_WhenNotEditing_DoesNothing()
        {
            var unityProvider = UnityProvider.Instance;
            var chord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.E, unityProvider)), unityProvider);
            var session = new HotkeyEditSession
            {
                State = HotkeyEditState.Idle
            };

            session.SetWorkingChord(chord, new EntryChangeSink());

            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.WorkingChord);
        }

        [Fact]
        public void SetWorkingChord_WhenEditing_ReplacesCurrentChordAndBeginsRecording()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            var originalChord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)), unityProvider);
            originalValue.Add(originalChord);
            var entry = CreateEntryBinding(originalValue);
            entry.SetupGet(x => x.Key).Returns(nameof(SetWorkingChord_WhenEditing_ReplacesCurrentChordAndBeginsRecording));
            var newChord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.B, unityProvider)), unityProvider);
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.BeginRecord(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.C, unityProvider)),
                unityProvider));
            session.State = HotkeyEditState.WaitingConfirm;
            session.PreviewGamepadChord = new GamepadChord(unityProvider);
            session.PreviewKeyboardChord = new KeyboardChord(unityProvider);

            session.SetWorkingChord(newChord, new EntryChangeSink());

            Assert.Same(entry.Object.Value, session.OriginalValue);
            Assert.Same(newChord, session.WorkingChord);
            Assert.Equal(HotkeyEditState.WaitingPress, session.State);
            Assert.False(Hotkey.GlobalValid);
            Assert.False(session.OriginalValue.Valid);
        }

        [Fact]
        public void AddChord_WhenNotEditing_DoesNothing()
        {
            var unityProvider = UnityProvider.Instance;
            var chord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.F, unityProvider)), unityProvider);
            var session = new HotkeyEditSession();

            session.AddChord(chord, new EntryChangeSink());

            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.WorkingValue);
            Assert.Null(session.WorkingChord);
        }

        [Fact]
        public void AddChord_WhenEditing_AddsChordAndBeginsRecording()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            originalValue.Add(new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)), unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var addedChord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.G, unityProvider)), unityProvider);
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.BeginRecord(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.H, unityProvider)),
                unityProvider));
            session.State = HotkeyEditState.WaitingConfirm;
            session.PreviewGamepadChord = new GamepadChord(unityProvider);
            session.PreviewKeyboardChord = new KeyboardChord(unityProvider);

            session.AddChord(addedChord, new EntryChangeSink());

            Assert.Contains(addedChord, session.WorkingValue.Hotkeys);
            Assert.Same(addedChord, session.WorkingChord);
            Assert.Equal(HotkeyEditState.WaitingPress, session.State);
        }

        [Fact]
        public void RemoveChord_WhenNotEditing_DoesNothing()
        {
            var unityProvider = UnityProvider.Instance;
            var chord = new HotkeyChord(new KeyboardChord(unityProvider, new KeyboardTrigger(Key.I, unityProvider)), unityProvider);
            var session = new HotkeyEditSession();

            session.RemoveChord(chord, new EntryChangeSink());

            Assert.Equal(HotkeyEditState.Idle, session.State);
            Assert.Null(session.WorkingValue);
        }

        [Fact]
        public void RemoveChord_WhenEditing_RemovesChordAndCommitsRemainingValue()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            originalValue.Add(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)),
                unityProvider));
            originalValue.Add(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.B, unityProvider)),
                unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var workingValue = originalValue.Clone();
            var removedChord = workingValue.Hotkeys[0];
            var session = new HotkeyEditSession
            {
                Entry = entry.Object,
                State = HotkeyEditState.Expanded,
                OriginalValue = originalValue,
                WorkingValue = workingValue
            };

            session.RemoveChord(removedChord, new EntryChangeSink());

            var committedValue = Assert.IsType<Hotkey>(entry.Object.Value);
            Assert.NotSame(originalValue, committedValue);
            Assert.Equal("B", committedValue.ToString());
            Assert.DoesNotContain(removedChord, committedValue.Hotkeys);
            Assert.Same(committedValue, session.OriginalValue);
            Assert.Equal("B", session.WorkingValue.ToString());
            Assert.False(session.WorkingValue.Valid);
            Assert.Null(session.WorkingChord);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void RemoveChord_WhenChordIsCurrentlyRecording_RemovesMatchingChordFromWorkingCopy()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            originalValue.Add(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)),
                unityProvider));
            originalValue.Add(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.B, unityProvider)),
                unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            var recordingChord = session.WorkingValue.Hotkeys[0];
            session.BeginRecord(recordingChord);

            session.RemoveChord(recordingChord, new EntryChangeSink());

            var committedValue = Assert.IsType<Hotkey>(entry.Object.Value);
            Assert.Equal("B", committedValue.ToString());
            Assert.DoesNotContain(committedValue.Hotkeys, chord => chord.ToString() == "A");
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void RemoveChord_WhenNewChordIsCurrentlyRecording_CancelsAdditionWithoutWritingEntry()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            originalValue.Add(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.A, unityProvider)),
                unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var addedChord = new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.B, unityProvider)),
                unityProvider);
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.WorkingValue.Add(addedChord);
            session.BeginRecord(addedChord);

            session.RemoveChord(addedChord, new EntryChangeSink());

            Assert.Same(originalValue, entry.Object.Value);
            Assert.Same(originalValue, session.OriginalValue);
            Assert.Equal("A", session.WorkingValue.ToString());
            Assert.DoesNotContain(session.WorkingValue.Hotkeys, chord => chord.ToString() == "B");
            Assert.Null(session.WorkingChord);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void RemoveChord_WhenItIsTheLastChord_PreservesOriginalValue()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            originalValue.Add(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.C, unityProvider)),
                unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var workingValue = originalValue.Clone();
            var session = new HotkeyEditSession
            {
                Entry = entry.Object,
                State = HotkeyEditState.Expanded,
                OriginalValue = originalValue,
                WorkingValue = workingValue
            };

            session.RemoveChord(workingValue.Hotkeys[0], new EntryChangeSink());

            Assert.Same(originalValue, entry.Object.Value);
            Assert.Same(originalValue, session.OriginalValue);
            Assert.Equal("C", session.WorkingValue.ToString());
            Assert.False(session.WorkingValue.Valid);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void ConfirmRecord_WhenWorkingValueIsUnchanged_DoesNotWriteEntry()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            originalValue.Add(new HotkeyChord(
                new KeyboardChord(unityProvider, new KeyboardTrigger(Key.D, unityProvider)),
                unityProvider));
            var entry = CreateEntryBinding(originalValue);
            var session = new HotkeyEditSession();

            session.BeginEdit(entry.Object);
            session.BeginRecord(new HotkeyChord(unityProvider));
            session.State = HotkeyEditState.WaitingConfirm;

            session.ConfirmRecord(new EntryChangeSink());

            entry.VerifySet(x => x.Value = It.IsAny<object>(), Times.Never);
            Assert.Same(originalValue, session.OriginalValue);
            Assert.Equal("D", session.WorkingValue.ToString());
            Assert.False(session.WorkingValue.Valid);
            Assert.Equal(HotkeyEditState.Expanded, session.State);
        }

        [Fact]
        public void DealWaitingPress_WhenNotEditingOrNotWaitingPress_DoesNothing()
        {
            var session = new HotkeyEditSession
            {
                State = HotkeyEditState.Expanded
            };

            session.DealWaitingPress();

            Assert.Equal(HotkeyEditState.Expanded, session.State);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }

        [Fact]
        public void DealWaitingPress_WhenNoInputPressed_LeavesStateUnchanged()
        {
            var unityProvider = UnityProvider.Instance;
            var entry = CreateEntryBinding(new Hotkey());
            var session = new HotkeyEditSession
            {
                Entry = entry.Object,
                State = HotkeyEditState.WaitingPress,
                WorkingValue = new Hotkey()
            };

            session.DealWaitingPress();

            Assert.Equal(HotkeyEditState.WaitingPress, session.State);
            Assert.Null(session.PreviewGamepadChord);
            Assert.Null(session.PreviewKeyboardChord);
        }




        [Fact]
        public void DealRecording_WhenNotEditingOrNotRecording_DoesNothing()
        {
            var session = new HotkeyEditSession
            {
                State = HotkeyEditState.Expanded,
                PreviewGamepadChord = new GamepadChord(UnityProvider.Instance),
                PreviewKeyboardChord = new KeyboardChord(UnityProvider.Instance),
                WorkingChord = new HotkeyChord(UnityProvider.Instance)
            };

            session.DealRecording();

            Assert.Equal(HotkeyEditState.Expanded, session.State);
            Assert.False(session.PreviewGamepadChord.IsValid);
            Assert.False(session.PreviewKeyboardChord.IsValid);
            Assert.False(session.WorkingChord.IsValid);
        }

        [Fact]
        public void DealRecording_WhenNoInputPressed_MovesToWaitingConfirm()
        {
            var unityProvider = UnityProvider.Instance;
            var session = CreateEditingSession(unityProvider, HotkeyEditState.Recording);

            session.DealRecording();

            Assert.Equal(HotkeyEditState.WaitingConfirm, session.State);
        }


        [Fact]
        public void DealWaitingConfirm_WhenNotEditingOrNotWaitingConfirm_DoesNothing()
        {
            var session = new HotkeyEditSession
            {
                State = HotkeyEditState.Recording,
                PreviewGamepadChord = new GamepadChord(UnityProvider.Instance),
                PreviewKeyboardChord = new KeyboardChord(UnityProvider.Instance),
                WorkingChord = new HotkeyChord(UnityProvider.Instance)
            };

            session.DealWaitingConfirm();

            Assert.Equal(HotkeyEditState.Recording, session.State);
            Assert.False(session.PreviewGamepadChord.IsValid);
            Assert.False(session.PreviewKeyboardChord.IsValid);
            Assert.False(session.WorkingChord.IsValid);
        }


        [Fact]
        public void DealWaitingConfirm_WhenWorkingChordIsInvalidAndNoInputPressed_ClearsPreviewsAndReturnsToRecording()
        {
            var unityProvider = UnityProvider.Instance;
            var session = CreateEditingSession(unityProvider, HotkeyEditState.WaitingConfirm);
            session.PreviewGamepadChord = new GamepadChord(unityProvider, new GamepadTrigger(GamepadButton.DpadUp, unityProvider));
            session.PreviewKeyboardChord = new KeyboardChord(unityProvider, new KeyboardTrigger(Key.B, unityProvider));
            session.WorkingChord = new HotkeyChord(unityProvider);

            session.DealWaitingConfirm();

            Assert.Equal(HotkeyEditState.Recording, session.State);
            Assert.False(session.PreviewGamepadChord.IsValid);
            Assert.False(session.PreviewKeyboardChord.IsValid);
        }

        [Fact]
        public void DealWaitingConfirm_WhenWorkingChordIsValidAndNoInputPressed_LeavesStateUnchanged()
        {
            var unityProvider = UnityProvider.Instance;
            var existingKeyboardPreview = new KeyboardChord(unityProvider, new KeyboardTrigger(Key.C, unityProvider));
            var existingGamepadPreview = new GamepadChord(unityProvider, new GamepadTrigger(GamepadButton.DpadUp, unityProvider));
            var workingChord = new HotkeyChord(existingKeyboardPreview, unityProvider);
            var session = CreateEditingSession(unityProvider, HotkeyEditState.WaitingConfirm);
            session.PreviewKeyboardChord = existingKeyboardPreview;
            session.PreviewGamepadChord = existingGamepadPreview;
            session.WorkingChord = workingChord;

            session.DealWaitingConfirm();

            Assert.Equal(HotkeyEditState.WaitingConfirm, session.State);
            Assert.Same(existingKeyboardPreview, session.PreviewKeyboardChord);
            Assert.Same(existingGamepadPreview, session.PreviewGamepadChord);
            Assert.Same(existingKeyboardPreview, session.WorkingChord.Chord);
        }

        [Fact]
        public void CaptureHotkeyInput_WhenEditingWithNoPressedInput_ReturnsEmptySnapshot()
        {
            var unityProvider = UnityProvider.Instance;
            var session = CreateEditingSession(unityProvider, HotkeyEditState.Recording);

            var result = session.CaptureHotkeyInput();

            Assert.NotNull(result);
            Assert.False(result.HasAnyPressed);
            Assert.False(result.GamepadChord.IsValid);
            Assert.False(result.KeyboardChord.IsValid);
        }


        private static HotkeyEditSession CreateEditingSession(UnityProvider unityProvider, HotkeyEditState state)
        {
            return new HotkeyEditSession
            {
                Entry = CreateEntryBinding(new Hotkey()).Object,
                State = state,
                WorkingValue = new Hotkey(),
                WorkingChord = new HotkeyChord(unityProvider),
                PreviewGamepadChord = new GamepadChord(unityProvider),
                PreviewKeyboardChord = new KeyboardChord(unityProvider)
            };
        }

        [Fact]
        public void CaptureHotkeyInput_WhenNotEditing_ReturnsNull()
        {
            var session = new HotkeyEditSession
            {
                State = HotkeyEditState.Idle
            };

            var result = session.CaptureHotkeyInput();

            Assert.Null(result);
        }

        [Fact]
        public void Update_WhenStateIsUnknown_LeavesSessionUntouched()
        {
            var unityProvider = UnityProvider.Instance;
            var originalValue = new Hotkey();
            var entry = CreateEntryBinding(originalValue);
            var workingValue = originalValue.Clone();
            var workingChord = new HotkeyChord(unityProvider);
            var session = new HotkeyEditSession
            {
                Entry = entry.Object,
                State = (HotkeyEditState)int.MaxValue,
                OriginalValue = originalValue,
                WorkingValue = workingValue,
                WorkingChord = workingChord
            };

            session.Update();

            Assert.Same(entry.Object, session.Entry);
            Assert.Equal((HotkeyEditState)int.MaxValue, session.State);
            Assert.Same(originalValue, session.OriginalValue);
            Assert.Same(workingValue, session.WorkingValue);
            Assert.Same(workingChord, session.WorkingChord);
        }


        private static Mock<IEntryBinding> CreateEntryBinding(object value)
        {
            var entry = new Mock<IEntryBinding>();
            entry.SetupGet(x => x.EditBuffer).Returns(new EntryEditBuffer());
            entry.SetupProperty(x => x.Value, value);
            return entry;
        }

        /// <summary>
        /// 清除当前测试对全局热键开关的修改，避免影响后续测试。
        /// </summary>
        public void Dispose()
        {
            Hotkey.GlobalValid = true;
        }
    }
}
