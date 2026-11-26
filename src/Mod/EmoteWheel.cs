using System;
using RDR2.Native;
using RDR2Coop.Net;
using Keys = System.Windows.Forms.Keys;

namespace RDR2Coop.Mod
{
    // Uses the existing PickList modal slot so keyboard prompts and other co-op menus retain ownership of input.
    sealed class EmoteWheel
    {
        const uint LookLR = 0xA987235F, LookUD = 0xD2047988;
        const uint FrontendAccept = 0xC7B5340A, FrontendCancel = 0x156F7119;
        const uint Attack = 0x07CE1E61, Aim = 0xF84FA74F, Sprint = 0x8FFC75D6, Reload = 0xE30CD707;

        readonly Keys _holdKey;
        readonly Func<bool> _canOpen;
        readonly Func<int, bool> _unavailable;
        readonly Action<int> _play;
        int _selected = -1;
        float _mouseX, _mouseY;
        bool _shown, _mouseSelected;

        public EmoteWheel(Keys holdKey, Func<bool> canOpen, Func<int, bool> unavailable, Action<int> play)
        {
            _holdKey = holdKey;
            _canOpen = canOpen;
            _unavailable = unavailable;
            _play = play;
        }

        public void OnKey(Keys key)
        {
            switch (key)
            {
                case Keys.Left:
                case Keys.Up:
                    Step(-1);
                    break;
                case Keys.Right:
                case Keys.Down:
                    Step(1);
                    break;
                case Keys.Enter:
                    Confirm();
                    break;
                case Keys.Escape:
                    PickList.Close();
                    break;
            }
        }

        public void OnKeyUp(Keys key)
        {
            if (_holdKey == Keys.None || key != _holdKey) return;
            if (_selected < 0) PickList.Close();
            else Confirm();
        }

        void Step(int change)
        {
            int count = EmoteList.All.Length;
            _selected = _selected < 0 ? (change < 0 ? count - 1 : 0) : (_selected + change + count) % count;
            _mouseX = _mouseY = 0;
            _mouseSelected = false;
        }

        void Confirm()
        {
            if (_selected < 0) return;
            PickList.Close();
            if (_canOpen()) _play(_selected);
        }

        public void Tick()
        {
            if (!_canOpen()) { PickList.Close(); return; }

            PAD.DISABLE_ALL_CONTROL_ACTIONS(0);
            bool keyboard = PAD.IS_USING_KEYBOARD_AND_MOUSE(0);
            // A button edge from the frame that opened the wheel belongs to the previous menu.
            if (_shown)
            {
                float x = PAD.GET_DISABLED_CONTROL_NORMAL(0, LookLR);
                float y = PAD.GET_DISABLED_CONTROL_NORMAL(0, LookUD);
                if (keyboard) MoveSelection(x, y);
                else
                {
                    _mouseX = _mouseY = 0;
                    _mouseSelected = false;
                    int slot = EmoteList.WheelSlot(x, y);
                    if (slot >= 0) _selected = slot;
                }
                if (Pressed(FrontendCancel) || Pressed(keyboard ? Aim : Reload)) { PickList.Close(); return; }
                if (_selected >= 0 && (Pressed(FrontendAccept) || Pressed(keyboard ? Attack : Sprint))) { Confirm(); return; }
            }
            _shown = true;
            string controls = keyboard
                ? (_holdKey == Keys.None
                    ? "Mouse / arrows to select  ·  Click / Enter to play  ·  Esc to close"
                    : $"Mouse / arrows to select  ·  Release {_holdKey} to play  ·  Esc to close")
                : "Right stick to select  ·  Confirm to play  ·  Back to close";
            EmoteWheelView.Draw(_selected, _unavailable, controls);
        }

        static bool Pressed(uint action) => PAD.IS_DISABLED_CONTROL_JUST_PRESSED(0, action);

        void MoveSelection(float x, float y)
        {
            if (Math.Abs(x) + Math.Abs(y) < 0.001f) return;
            _mouseX += x * 2.5f;
            _mouseY += y * 2.5f;
            float magnitude = (float)Math.Sqrt(_mouseX * _mouseX + _mouseY * _mouseY);
            if (magnitude > 1f) { _mouseX /= magnitude; _mouseY /= magnitude; }
            int slot = EmoteList.WheelSlot(_mouseX, _mouseY);
            if (slot >= 0) _mouseSelected = true;
            if (_mouseSelected) _selected = slot;
        }
    }
}
