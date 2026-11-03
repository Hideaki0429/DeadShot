// 【役割】変更可能なキーボード割り当ての保存・復元と重複判定を行う。
using System;
using UnityEngine;

// Only gameplay keys are rebindable. UI confirmation/cancellation remain available.
public static class KeyboardBindingStore
{
    public sealed class Entry
    {
        public readonly string field, label;
        public Entry(string field, string label)
        {
            this.field = field;
            this.label = label;
        }
    }

    public static readonly Entry[] Entries =
    {
        new Entry("forward", "前進"),
        new Entry("back", "後退"),
        new Entry("left", "左へ移動"),
        new Entry("right", "右へ移動"),
        new Entry("run", "走る"),
        new Entry("aim", "武器を構える／ライト"),
        new Entry("attack", "攻撃／アイテム取得"),
        new Entry("reload", "リロード"),
        new Entry("changeView", "視点切り替え"),
        new Entry("changeZoom", "ズーム倍率"),
        new Entry("heal", "回復"),
        new Entry("map", "全体マップ"),
        new Entry("items", "アイテム画面"),
        new Entry("pause", "ポーズメニュー"),
        new Entry("weaponUp", "武器選択：上"),
        new Entry("weaponDown", "武器選択：下"),
        new Entry("weaponLeft", "武器選択：左"),
        new Entry("weaponRight", "武器選択：右"),
        new Entry("weaponQuickLeft", "武器即時選択：左"),
        new Entry("weaponQuickUp", "武器即時選択：上"),
        new Entry("weaponQuickDown", "武器即時選択：下"),
        new Entry("weaponQuickRight", "武器即時選択：右"),
        new Entry("weaponMenu", "武器選択パネル")
    };
    const string Prefix = "DeadShot.Keyboard.v1.";
    static KeyCode[] defaults;
    public static KeyCode[] Snapshot(GameInputManager manager)
    {
        var result = new KeyCode[Entries.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (KeyCode)typeof(GameInputManager).GetField(Entries[i].field).GetValue(manager);
        }

        return result;
    }

    public static KeyCode[] Defaults => defaults == null ? null : (KeyCode[])defaults.Clone();

    public static bool Allowed(KeyCode key) => key != KeyCode.None && key != KeyCode.Escape && Enum.IsDefined(typeof(KeyCode), key) && (int)key < (int)KeyCode.JoystickButton0;
    public static void Initialize(GameInputManager manager)
    {
        // Capture Inspector/prefab defaults BEFORE loading the player's saved configuration.
        defaults = Snapshot(manager);
        var saved = (KeyCode[])defaults.Clone();
        for (int i = 0; i < saved.Length; i++)
        {
            saved[i] = (KeyCode)PlayerPrefs.GetInt(Prefix + Entries[i].field, (int)defaults[i]);
            // 旧版の初期矢印だけをWASDへ移行。ユーザーが変更した値は維持する。
            if ((Entries[i].field == "weaponUp" && saved[i] == KeyCode.UpArrow) || (Entries[i].field == "weaponDown" && saved[i] == KeyCode.DownArrow) || (Entries[i].field == "weaponLeft" && saved[i] == KeyCode.LeftArrow) || (Entries[i].field == "weaponRight" && saved[i] == KeyCode.RightArrow) || (Entries[i].field == "changeView" && saved[i] == KeyCode.E) || (Entries[i].field == "weaponQuickLeft" && saved[i] == KeyCode.R) || (Entries[i].field == "weaponQuickDown" && saved[i] == KeyCode.Y) || (Entries[i].field == "weaponQuickRight" && saved[i] == KeyCode.U))
            {
                saved[i] = defaults[i];
            }
        }

        if (Validate(saved, out _))
        {
            Set(manager, saved);
        }
        else
        {
            Debug.LogWarning("[KeyBindings] 保存データが不正なため初期設定を使用します。");
        }
    }

    public static bool Validate(KeyCode[] keys, out string error)
    {
        if (keys == null || keys.Length != Entries.Length)
        {
            error = "設定数が一致しません。";
            return false;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            if (!Allowed(keys[i]))
            {
                error = "このキーは割り当てできません。";
                return false;
            }

            for (int j = 0; j < i; j++)
            {
                if (keys[i] == keys[j] && !CanShare(i, j))
                {
                    error = "「" + Entries[j].label + "」とキーが重複しています。";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }

    // 移動と武器選択パネル内の方向操作は別コンテキストなのでWASDを共有できる。
    public static bool CanShare(int first, int second)
    {
        string a = Entries[first].field, b = Entries[second].field;
        return Pair(a, b, "forward", "weaponUp") || Pair(a, b, "back", "weaponDown") || Pair(a, b, "left", "weaponLeft") || Pair(a, b, "right", "weaponRight");
    }

    static bool Pair(string a, string b, string x, string y) => (a == x && b == y) || (a == y && b == x);
    static void Set(GameInputManager manager, KeyCode[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            typeof(GameInputManager).GetField(Entries[i].field).SetValue(manager, keys[i]);
        }
    }

    public static bool Apply(GameInputManager manager, KeyCode[] keys, out string error)
    {
        if (!Validate(keys, out error))
        {
            return false;
        }

        Set(manager, keys);
        for (int i = 0; i < keys.Length; i++)
        {
            PlayerPrefs.SetInt(Prefix + Entries[i].field, (int)keys[i]);
        }

        PlayerPrefs.Save();
        manager.SetPaused(true); // Held keys must be released before returning to gameplay.
        return true;
    }

    public static string Display(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0:
                return "左クリック";
            case KeyCode.Mouse1:
                return "右クリック";
            case KeyCode.Mouse2:
                return "中央クリック";
            case KeyCode.LeftShift:
                return "L Shift";
            case KeyCode.RightShift:
                return "R Shift";
            case KeyCode.LeftControl:
                return "L Ctrl";
            case KeyCode.RightControl:
                return "R Ctrl";
            case KeyCode.Return:
                return "Enter";
            case KeyCode.UpArrow:
                return "↑";
            case KeyCode.DownArrow:
                return "↓";
            case KeyCode.LeftArrow:
                return "←";
            case KeyCode.RightArrow:
                return "→";
            case KeyCode.BackQuote:
                return "`";
            case KeyCode.Minus:
                return "-";
            case KeyCode.Equals:
                return "=";
            case KeyCode.LeftBracket:
                return "[";
            case KeyCode.RightBracket:
                return "]";
            case KeyCode.Backslash:
                return "\\";
            case KeyCode.Semicolon:
                return ";";
            case KeyCode.Quote:
                return "'";
            case KeyCode.Comma:
                return ",";
            case KeyCode.Period:
                return ".";
            case KeyCode.Slash:
                return "/";
            case KeyCode.CapsLock:
                return "Caps";
        }

        return key.ToString().Replace("Alpha", "");
    }
}
