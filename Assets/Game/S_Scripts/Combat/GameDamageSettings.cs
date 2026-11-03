// 【役割】武器と命中部位に応じたダメージ設定を保持・参照する。
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dead Shot/Damage Settings", fileName = "DeadShotDamageSettings")]
public sealed class GameDamageSettings : ScriptableObject
{
    public const string ResourceName = "DeadShotDamageSettings";
    [Serializable]
    public sealed class WeaponDamageProfile
    {
        [Tooltip("handgun / smg / knife / grenade など、射撃側と同じID")]
        public string weaponId = "handgun";
        [Min(0)]
        public int baseDamage = 100;
        [Tooltip("オフなら全部位へBase Damageをそのまま使用")]
        public bool usePartMultipliers = true;
        [Min(0f)]
        public float head = 1f;
        [Min(0f)]
        public float torso = 1f;
        [Min(0f)]
        public float leftArm = 1f;
        [Min(0f)]
        public float rightArm = 1f;
        [Min(0f)]
        public float leftLeg = 1f;
        [Min(0f)]
        public float rightLeg = 1f;
        public int GetDamage(EnemyHealth.HitPart part)
        {
            return GetDamage(part, baseDamage);
        }

        public int GetDamage(EnemyHealth.HitPart part, int resolvedBaseDamage)
        {
            resolvedBaseDamage = Mathf.Max(0, resolvedBaseDamage);
            if (!usePartMultipliers)
            {
                return resolvedBaseDamage;
            }

            float multiplier;
            switch (part)
            {
                case EnemyHealth.HitPart.Head:
                    multiplier = head;
                    break;
                case EnemyHealth.HitPart.Torso:
                    multiplier = torso;
                    break;
                case EnemyHealth.HitPart.LeftArm:
                    multiplier = leftArm;
                    break;
                case EnemyHealth.HitPart.RightArm:
                    multiplier = rightArm;
                    break;
                case EnemyHealth.HitPart.LeftLeg:
                    multiplier = leftLeg;
                    break;
                case EnemyHealth.HitPart.RightLeg:
                    multiplier = rightLeg;
                    break;
                default:
                    multiplier = 1f;
                    break;
            }

            return Mathf.Max(0, Mathf.RoundToInt(resolvedBaseDamage * multiplier));
        }
    }

    [Header("体力")]
    [Min(1)]
    public int playerMaxHP = 500;
    [Min(1)]
    public int zombieMaxHP = 200;
    [Header("プレイヤー武器・アイテム")]
    [Tooltip("武器を増やす場合は同じ形式で追加。部位倍率も武器ごとに設定可能")]
    public List<WeaponDamageProfile> weaponDamageProfiles = new List<WeaponDamageProfile>();
    [Header("特殊ゴーグル弱点")]
    [Min(1f)]
    public float weakPointMaxHpMultiplier = 1f;
    [Header("ゾンビ攻撃")]
    [Min(0)]
    public int zombieStrikeDamage = 100;
    [Min(0)]
    public int zombieBiteDamage = 200;
    [Min(0)]
    public int zombieCrawlDamage = 100;
    [Header("通常撃破スコア（最後に命中した部位）")]
    [Min(0)]
    public int headKillScore = 200;
    [Min(0)]
    public int torsoKillScore = 100;
    [Min(0)]
    public int leftArmKillScore = 100;
    [Min(0)]
    public int rightArmKillScore = 100;
    [Min(0)]
    public int leftLegKillScore = 100;
    [Min(0)]
    public int rightLegKillScore = 100;
    [Header("弱点撃破スコア")]
    [Min(0)]
    public int weakPointKillScore = 300;
    // 旧シーン／Prefabとの互換用。Inspectorには重複表示しない。
    [HideInInspector]
    public int handgunDamage = 100;
    [HideInInspector]
    public int grenadeDamage;
    [HideInInspector]
    public float grenadeHandgunMultiplier = 2f;
    public int GetWeaponDamage(string weaponId, EnemyHealth.HitPart part)
    {
        WeaponDamageProfile profile = FindWeapon(weaponId);
        if (profile != null)
        {
            return profile.GetDamage(part);
        }

        return string.Equals(weaponId, "grenade", StringComparison.OrdinalIgnoreCase) ? GetGrenadeDamage() : handgunDamage;
    }

    public int GetWeaponDamage(string weaponId, EnemyHealth.HitPart part, int baseDamageOverride)
    {
        WeaponDamageProfile profile = FindWeapon(weaponId);
        if (profile != null)
        {
            return profile.GetDamage(part, baseDamageOverride >= 0 ? baseDamageOverride : profile.baseDamage);
        }

        return baseDamageOverride >= 0 ? baseDamageOverride : GetWeaponDamage(weaponId, part);
    }

    public int GetGrenadeDamage()
    {
        WeaponDamageProfile profile = FindWeapon("grenade");
        if (profile != null)
        {
            return profile.baseDamage;
        }

        return grenadeDamage > 0 ? grenadeDamage : Mathf.RoundToInt(handgunDamage * grenadeHandgunMultiplier);
    }

    public int GetWeakPointDamage(int enemyMaxHP) => Mathf.Max(1, Mathf.CeilToInt(enemyMaxHP * Mathf.Max(1f, weakPointMaxHpMultiplier)));
    public int GetKillScore(EnemyHealth.HitPart part, bool weakPointKill)
    {
        if (weakPointKill)
        {
            return weakPointKillScore;
        }

        switch (part)
        {
            case EnemyHealth.HitPart.Head:
                return headKillScore;
            case EnemyHealth.HitPart.Torso:
                return torsoKillScore;
            case EnemyHealth.HitPart.LeftArm:
                return leftArmKillScore;
            case EnemyHealth.HitPart.RightArm:
                return rightArmKillScore;
            case EnemyHealth.HitPart.LeftLeg:
                return leftLegKillScore;
            case EnemyHealth.HitPart.RightLeg:
                return rightLegKillScore;
            default:
                return torsoKillScore;
        }
    }

    public bool EnsureDefaultWeaponProfiles()
    {
        if (weaponDamageProfiles == null)
        {
            weaponDamageProfiles = new List<WeaponDamageProfile>();
        }

        bool changed = false;
        changed |= AddMissing("handgun", handgunDamage > 0 ? handgunDamage : 100, true);
        changed |= AddMissing("smg", 35, true);
        changed |= AddMissing("knife", 80, true);
        changed |= AddMissing("grenade", grenadeDamage > 0 ? grenadeDamage : 200, false);
        return changed;
    }

    private bool AddMissing(string id, int damage, bool useParts)
    {
        if (FindWeapon(id) != null)
        {
            return false;
        }

        weaponDamageProfiles.Add(new WeaponDamageProfile { weaponId = id, baseDamage = damage, usePartMultipliers = useParts });
        return true;
    }

    private WeaponDamageProfile FindWeapon(string id) => weaponDamageProfiles?.Find(profile => profile != null && string.Equals(profile.weaponId, id, StringComparison.OrdinalIgnoreCase));
    public static GameDamageSettings Load()
    {
        GameDamageSettings settings = Resources.Load<GameDamageSettings>(ResourceName);
        if (settings != null)
        {
            settings.EnsureDefaultWeaponProfiles();
        }

        return settings;
    }
}
