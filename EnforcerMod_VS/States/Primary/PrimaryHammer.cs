﻿using RoR2;
using System;
using UnityEngine;

namespace EntityStates.Enforcer.NeutralSpecial {
    public class HammerSwing : BaseSkillState {
        public static float baseDuration = 1f;
        public static float baseShieldDuration = 1.2f;
        public static float damageCoefficient = 5f;
        public static float shieldDamageCoefficient = 10f;

        public static float baseDownwardForce = 480;
        public static float shieldDownwardForce = 800;
        public static float basePushawayForce = 120;
        public static float shieldPushawayForce = 160;

        public static GameObject slamEffectPrefab = EnforcerPlugin.EnforcerModPlugin.hammerSlamEffect; // Resources.Load<GameObject>("Prefabs/Effects/ImpactEffects/ParentSlamEffect");
        public static GameObject shieldSlamEffectPrefab = Resources.Load<GameObject>("Prefabs/Effects/ImpactEffects/ParentSlamEffect");

        public static float baseEarlyExitTime = 0.78f;
        public static float procCoefficient = 1f;
        public static float attackRecoil = 1.15f;
        public static float hitHopVelocity = 5.5f;

        private float duration;
        private float damage;
        private float downwardForce;
        private float pushawayForce;
        private GameObject slamPrefab;
        private float earlyExitDuration;
        private string hitboxString;

        private ChildLocator childLocator;
        private bool hasFired;
        private float hitPauseTimer;
        private OverlapAttack attack;
        private bool inHitPause;
        private bool hasHopped;
        private float stopwatch;
        private Animator animator;
        private HitStopCachedState hitStopCachedState;
        private Transform modelBaseTransform;

        public override void OnEnter() {
            base.OnEnter();
            StartAimMode(2f, false);
            hasFired = false;
            characterBody.isSprinting = false;

            childLocator = GetModelChildLocator();
            modelBaseTransform = GetModelBaseTransform();
            animator = GetModelAnimator();
            bool grounded = characterMotor.isGrounded;

            if (HasBuff(EnforcerPlugin.Modules.Buffs.protectAndServeBuff) || HasBuff(EnforcerPlugin.Modules.Buffs.energyShieldBuff)) {
                duration = baseShieldDuration / attackSpeedStat;
                damage = shieldDamageCoefficient;
                hitboxString = "HammerBig";
                downwardForce = shieldDownwardForce;
                pushawayForce = shieldPushawayForce;
                slamPrefab = shieldSlamEffectPrefab;

                PlayCrossfade("Gesture, Additive", "SwingHammer", "HammerSwing.playbackRate", duration, 0.05f);

            } else {
                duration = baseDuration / attackSpeedStat;
                damage = damageCoefficient;
                hitboxString = "Hammer";
                downwardForce = baseDownwardForce;
                pushawayForce = basePushawayForce;
                slamPrefab = slamEffectPrefab;

                PlayCrossfade("Gesture, Additive", "SwingHammer", "HammerSwing.playbackRate", duration, 0.05f);
            }

            earlyExitDuration = duration * baseEarlyExitTime;

            HitBoxGroup hitBoxGroup = FindHitBoxGroup(hitboxString);

            attack = new OverlapAttack();
            attack.damageType = DamageType.Generic;
            attack.attacker = gameObject;
            attack.inflictor = gameObject;
            attack.teamIndex = GetTeam();
            attack.damage = damage * damageStat;
            attack.procCoefficient = 1;
            attack.hitEffectPrefab = EnforcerPlugin.Assets.hammerImpactFX;
            attack.forceVector = Vector3.down * downwardForce;
            attack.pushAwayForce = pushawayForce;
            attack.hitBoxGroup = hitBoxGroup;
            attack.isCrit = RollCrit();
            attack.impactSound = EnforcerPlugin.Assets.hammerHitSoundEvent.index;
        }

        public override void FixedUpdate() {
            base.FixedUpdate();

            hitPauseTimer -= Time.fixedDeltaTime;

            if (hitPauseTimer <= 0f && inHitPause) {
                ConsumeHitStopCachedState(hitStopCachedState, characterMotor, animator);
                inHitPause = false;
            }

            if (!inHitPause) {
                stopwatch += Time.fixedDeltaTime;
            } else {
                if (characterMotor) characterMotor.velocity = Vector3.zero;
                if (animator) animator.SetFloat("HammerSwing.playbackRate", 0f);
            }

            if (stopwatch >= duration * 0.469f && stopwatch <= duration * 0.6) {
                FireAttack();
            }

            if (fixedAge >= earlyExitDuration && inputBank.skill1.down && isAuthority) {
                outer.SetNextState(new HammerSwing());
                return;
            }

            if (fixedAge >= duration && isAuthority) {
                outer.SetNextStateToMain();
                return;
            }
        }

        public void FireAttack() {
            if (!hasFired) {
                hasFired = true;

                Util.PlayAttackSpeedSound(EnforcerPlugin.Sounds.NemesisSwing, gameObject, 0.25f + attackSpeedStat);

                AddRecoil(-1f * attackRecoil, -2f * attackRecoil, -0.5f * attackRecoil, 0.5f * attackRecoil);

                string muzzleString = "ShieldHitbox";
                EffectManager.SimpleMuzzleFlash(EnforcerPlugin.Assets.hammerSwingFX, gameObject, muzzleString, true);

                if (slamPrefab) {
                    Vector3 sex = childLocator.FindChild("HammerMuzzle").transform.position;
                    
                    EffectData effectData = new EffectData();
                    effectData.origin = sex - Vector3.up;
                    effectData.scale = TestValueManager.testValue;

                    EffectManager.SpawnEffect(slamPrefab, effectData, true);
                }
            }

            if (isAuthority) {
                if (attack.Fire()) {
                    if (!hasHopped) {
                        if (characterMotor && !characterMotor.isGrounded) {
                            SmallHop(characterMotor, hitHopVelocity);
                        }

                        hasHopped = true;
                    }

                    if (!inHitPause) {
                        hitStopCachedState = CreateHitStopCachedState(characterMotor, animator, "HammerSwing.playbackRate");
                        hitPauseTimer = 1.5f * Merc.GroundLight.hitPauseDuration / attackSpeedStat;
                        inHitPause = true;
                    }
                }
            }
        }

        public override void OnExit() {
            if (!hasFired) FireAttack();

            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() {
            return InterruptPriority.Skill;
        }
    }
}