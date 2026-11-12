// Probably Doom
// Copyright (C) 2026 dbfoxtw
// BuildTicCmd 照 ManagedDoom 上游的 Silk/SilkUserInput.cs 改寫（Copyright (C) 2019-2020 Nobuaki Tanaka，GPL v2 or later），拿掉滑鼠的部分。
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

namespace ProbablyDoom
{
    using System.Collections.Generic;
    using ManagedDoom;
    using ManagedDoom.UserInput;

    /// <summary>
    /// Doom 的鍵盤（IUserInput）：記住哪些 Doom 鍵按著，按下、放開時送 DoomEvent 給 Doom（選單、作弊碼、存檔名稱用），
    /// 玩家的移動由 BuildTicCmd 照按著的鍵與設定的按鍵綁定算（同上游 SilkUserInput）。**只用鍵盤**（需求.md 決定 10）：
    /// 滑鼠一直是遊戲的，綁定裡的滑鼠鍵不看、也不做滑鼠轉向。
    /// 不碰 Unity：Unity 的鍵怎麼對到 Doom 鍵在 UnityKeyboard（宿主），這裡只管 Doom 這邊，離線測試測同一份。
    ///
    /// 同一個 Doom 鍵可能對到好幾個 Unity 鍵（Enter 與小鍵盤的 Enter），所以用計數：從 0 變 1 才送按下、回到 0 才送放開。
    /// </summary>
    internal sealed class DoomKeyboard : IUserInput
    {
        readonly Config _config;
        readonly int[] _held = new int[(int)DoomKey.Count];
        readonly bool[] _weaponKeys = new bool[7];
        int _turnHeld;

        internal DoomKeyboard(Config config)
        {
            _config = config;
        }

        /// <summary>一個 Unity 鍵按下了：對到的 Doom 鍵從沒按變成按著時回傳 true（呼叫端送 KeyDown）。</summary>
        internal bool Press(DoomKey key)
        {
            if (!Valid(key)) return false;
            return ++_held[(int)key] == 1;
        }

        /// <summary>一個 Unity 鍵放開了：對到的 Doom 鍵變成沒按時回傳 true（呼叫端送 KeyUp）。</summary>
        internal bool Release(DoomKey key)
        {
            if (!Valid(key) || _held[(int)key] == 0) return false;
            return --_held[(int)key] == 0;
        }

        /// <summary>全部放開（失去焦點時）：回傳原本按著的 Doom 鍵，呼叫端逐一送 KeyUp，免得角色一直走、一直開火。</summary>
        internal List<DoomKey> ReleaseAll()
        {
            var released = new List<DoomKey>();
            for (var i = 0; i < _held.Length; i++)
            {
                if (_held[i] > 0) released.Add((DoomKey)i);
                _held[i] = 0;
            }
            _turnHeld = 0;
            return released;
        }

        internal bool IsDown(DoomKey key) => Valid(key) && _held[(int)key] > 0;

        static bool Valid(DoomKey key) => key >= 0 && key < DoomKey.Count;

        bool IsPressed(KeyBinding binding)
        {
            foreach (var key in binding.Keys)
                if (IsDown(key)) return true;
            return false;
        }

        public void BuildTicCmd(TicCmd cmd)
        {
            var keyForward = IsPressed(_config.key_forward);
            var keyBackward = IsPressed(_config.key_backward);
            var keyStrafeLeft = IsPressed(_config.key_strafeleft);
            var keyStrafeRight = IsPressed(_config.key_straferight);
            var keyTurnLeft = IsPressed(_config.key_turnleft);
            var keyTurnRight = IsPressed(_config.key_turnright);
            var keyFire = IsPressed(_config.key_fire);
            var keyUse = IsPressed(_config.key_use);
            var keyRun = IsPressed(_config.key_run);
            var keyStrafe = IsPressed(_config.key_strafe);

            _weaponKeys[0] = IsDown(DoomKey.Num1);
            _weaponKeys[1] = IsDown(DoomKey.Num2);
            _weaponKeys[2] = IsDown(DoomKey.Num3);
            _weaponKeys[3] = IsDown(DoomKey.Num4);
            _weaponKeys[4] = IsDown(DoomKey.Num5);
            _weaponKeys[5] = IsDown(DoomKey.Num6);
            _weaponKeys[6] = IsDown(DoomKey.Num7);

            cmd.Clear();

            var strafe = keyStrafe;
            var speed = keyRun ? 1 : 0;
            var forward = 0;
            var side = 0;

            if (_config.game_alwaysrun)
            {
                speed = 1 - speed;
            }

            if (keyTurnLeft || keyTurnRight)
            {
                _turnHeld++;
            }
            else
            {
                _turnHeld = 0;
            }

            int turnSpeed;
            if (_turnHeld < PlayerBehavior.SlowTurnTics)
            {
                turnSpeed = 2;
            }
            else
            {
                turnSpeed = speed;
            }

            if (strafe)
            {
                if (keyTurnRight)
                {
                    side += PlayerBehavior.SideMove[speed];
                }
                if (keyTurnLeft)
                {
                    side -= PlayerBehavior.SideMove[speed];
                }
            }
            else
            {
                if (keyTurnRight)
                {
                    cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
                if (keyTurnLeft)
                {
                    cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
            }

            if (keyForward)
            {
                forward += PlayerBehavior.ForwardMove[speed];
            }
            if (keyBackward)
            {
                forward -= PlayerBehavior.ForwardMove[speed];
            }

            if (keyStrafeLeft)
            {
                side -= PlayerBehavior.SideMove[speed];
            }
            if (keyStrafeRight)
            {
                side += PlayerBehavior.SideMove[speed];
            }

            if (keyFire)
            {
                cmd.Buttons |= TicCmdButtons.Attack;
            }

            if (keyUse)
            {
                cmd.Buttons |= TicCmdButtons.Use;
            }

            // Check weapon keys.
            for (var i = 0; i < _weaponKeys.Length; i++)
            {
                if (_weaponKeys[i])
                {
                    cmd.Buttons |= TicCmdButtons.Change;
                    cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
                    break;
                }
            }

            // 上游在這裡加上滑鼠的移動；Probably Doom 只用鍵盤，沒有這段

            if (forward > PlayerBehavior.MaxMove)
            {
                forward = PlayerBehavior.MaxMove;
            }
            else if (forward < -PlayerBehavior.MaxMove)
            {
                forward = -PlayerBehavior.MaxMove;
            }
            if (side > PlayerBehavior.MaxMove)
            {
                side = PlayerBehavior.MaxMove;
            }
            else if (side < -PlayerBehavior.MaxMove)
            {
                side = -PlayerBehavior.MaxMove;
            }

            cmd.ForwardMove += (sbyte)forward;
            cmd.SideMove += (sbyte)side;
        }

        // 滑鼠：不抓、不轉向（需求.md 決定 10）。Doom 的選單仍會顯示滑鼠靈敏度，改了沒有作用。
        public void Reset() { }
        public void GrabMouse() { }
        public void ReleaseMouse() { }
        public int MaxMouseSensitivity => 15;

        public int MouseSensitivity
        {
            get => _config.mouse_sensitivity;
            set => _config.mouse_sensitivity = value;
        }
    }
}
