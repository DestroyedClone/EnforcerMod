﻿using System;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using R2API;
using R2API.Utils;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using KinematicCharacterController;
using EntityStates.Enforcer;
using RoR2.Projectile;
using System.Collections;
using System.IO;

namespace EnforcerPlugin
{
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(MODUID, "Enforcer", "0.0.4")]
    [R2APISubmoduleDependency(new string[]
    {
        "PrefabAPI",
        "SurvivorAPI",
        "LoadoutAPI",
        "BuffAPI",
        "LanguageAPI",
        "SoundAPI",
        "EffectAPI"
    })]

    public class EnforcerPlugin : BaseUnityPlugin
    {
        public const string MODUID = "com.ok.Enforcer";

        public static EnforcerPlugin instance;

        //i didn't want this to be static considering we're using an instance now but it throws 23 errors if i remove the static modifier 
        //i'm not dealing with that
        public static GameObject characterPrefab;
        public static GameObject characterDisplay;

        public static GameObject bulletTracer;
        public static GameObject laserTracer;
        public static GameObject projectilePrefab;
        public GameObject tearGasPrefab;
        public static GameObject stunGrenade;

        public static GameObject blockEffectPrefab;

        public GameObject doppelganger;
        
        public static event Action awake;
        public static event Action start;

        public static readonly Color characterColor = new Color(0.26f, 0.27f, 0.46f);

        public static BuffIndex jackBoots;
        public static BuffIndex energyShieldBuff;
        public static BuffIndex tearGasDebuff;

        public static SkillDef shieldDownDef;//skilldef used while shield is down
        public static SkillDef shieldUpDef;//skilldef used while shield is up
        public static SkillDef shieldOffDef;//skilldef used while shield is off
        public static SkillDef shieldOnDef;//skilldef used while shield is on

        public static bool cum; //don't ask
        public static bool harbCrateInstalled = false;

        //更新许可证 DO WHAT THE FUCK YOU WANT TO

        public SkillLocator skillLocator;

        public EnforcerPlugin() {
            //don't touch this
            // what does all this even do anyway?
            //its our plugin constructor
            awake += EnforcerPlugin_Load;
            start += EnforcerPlugin_LoadStart;
        }

        private void EnforcerPlugin_Load()
        {
            //touch this all you want tho
            Assets.PopulateAssets();
            CreateDisplayPrefab();
            CreatePrefab();
            RegisterCharacter();
            Skins.RegisterSkins();
            ItemDisplays.RegisterDisplays();
            RegisterBuffs();
            RegisterProjectile();
            CreateDoppelganger();
            Hook();
        }

        private void EnforcerPlugin_LoadStart()
        {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.harbingerofme.HarbCrate"))
            {
                harbCrateInstalled = true;
                //ItemDisplays.RegisterHarbCrateDisplays();
                //i'll get back to this later, shit's not working
            }
        }

        public void Awake()
        {
            Action awake = EnforcerPlugin.awake;
            if (awake == null)
            {
                return;
            }
            awake();
        }
        public void Start()
        {
            Action start = EnforcerPlugin.start;
            if (start == null)
            {
                return;
            }
            start();
        }
        private void Hook() {
            //add hooks here
            //using this approach means we'll only ever have to comment one line if we don't want a hook to fire
            //it's much simpler this way, trust me
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            //On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnEnemyHit;
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
            On.RoR2.CharacterBody.Update += CharacterBody_Update;
            On.RoR2.BodyCatalog.SetBodyPrefabs += BodyCatalog_SetBodyPrefabs;
        }

        private void BodyCatalog_SetBodyPrefabs(On.RoR2.BodyCatalog.orig_SetBodyPrefabs orig, GameObject[] newBodyPrefabs)
        {
            //nicely done brother
            for (int i = 0; i < newBodyPrefabs.Length; i++) {
                if (newBodyPrefabs[i].name == "EnforcerBody" && newBodyPrefabs[i] != characterPrefab) {
                    newBodyPrefabs[i].name = "OldEnforcerBody";
                }
            }
            orig(newBodyPrefabs);
        }
        #region Hooks
        private void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            // the energy shield thing was causing some wierd bugs. Need to find a better solution that just canceling this method lol

            // it works for now

            if (self.name == "EnergyShield") return;

            orig(self);
            if (self)
            {
                if (self.HasBuff(jackBoots))
                {
                    Reflection.SetPropertyValue<int>(self, "maxJumpCount", 0);
                    Reflection.SetPropertyValue<float>(self, "armor", self.armor + 20);
                    Reflection.SetPropertyValue<float>(self, "moveSpeed", self.moveSpeed * 0.5f);
                }

                if (self.HasBuff(energyShieldBuff))
                {
                    Reflection.SetPropertyValue<int>(self, "maxJumpCount", 0);
                    Reflection.SetPropertyValue<float>(self, "armor", self.armor + 40);
                    Reflection.SetPropertyValue<float>(self, "moveSpeed", self.moveSpeed * 0.65f);
                }

                if (self.HasBuff(tearGasDebuff))
                {
                    Reflection.SetPropertyValue<int>(self, "maxJumpCount", 0);
                    Reflection.SetPropertyValue<float>(self, "armor", self.armor - 20);
                    Reflection.SetPropertyValue<float>(self, "moveSpeed", self.moveSpeed * 0.25f);
                    Reflection.SetPropertyValue<float>(self, "attackSpeed", self.attackSpeed * 0.75f);
                }
            }
        }
        private void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo info)
        {
            if ((info.damageType & DamageType.BarrierBlocked) != DamageType.Generic && self.body.baseNameToken == "ENFORCER_NAME")
            {
                string soundString = Sounds.ShieldBlockLight;
                if (info.procCoefficient >= 1) soundString = Sounds.ShieldBlockHeavy;

                Util.PlaySound(soundString, self.gameObject);

                EffectData effectData = new EffectData
                {
                    origin = info.position,
                    rotation = Util.QuaternionSafeLookRotation((info.force != Vector3.zero) ? info.force : UnityEngine.Random.onUnitSphere)
                };

                EffectManager.SpawnEffect(EnforcerPlugin.blockEffectPrefab, effectData, true);

                info.rejected = true;
            }

            /*ShieldComponent shieldComponent = self.GetComponent<ShieldComponent>();
            if (shieldComponent && info.attacker && self.body.HasBuff(jackBoots))
            {
                bool canBlock = GetShieldBlock(self, info, shieldComponent);

                //fix for blood shrines
                if (info.damageType.HasFlag(DamageType.BypassArmor)) canBlock = false;

                //fuck you no blocking void reavers. go to brazil cunt
                // it probably kills anyway but couldn't hurt to check
                if (info.damageType.HasFlag(DamageType.VoidDeath)) canBlock = false;

                if (canBlock)
                {
                    string soundString = Sounds.ShieldBlockLight;
                    if (info.damage >= (0.5f * self.fullCombinedHealth)) soundString = Sounds.ShieldBlockHeavy;

                    DamageNumberManager.instance.SpawnDamageNumber(0, info.position, false, self.body.teamComponent.teamIndex, DamageColorIndex.CritHeal);

                    Util.PlaySound(soundString, self.gameObject);

                    return;
                }
            }*/

            if (self.body.name == "EnergyShield")
            {
                info.damage = info.procCoefficient;
            }

            orig(self, info);
        }

        /*private bool GetShieldBlock(HealthComponent self, DamageInfo info, ShieldComponent shieldComponent) {
            CharacterBody charB = self.GetComponent<CharacterBody>();
            Ray aimRay = shieldComponent.aimRay;
            Vector3 relativePosition = info.attacker.transform.position - aimRay.origin;
            float angle = Vector3.Angle(shieldComponent.shieldDirection, relativePosition);

            return angle < ShieldBlockAngle;
        }*/

        /*private void GlobalEventManager_OnEnemyHit(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo info, GameObject victim)
        {
            ShieldComponent shieldComponent = self.GetComponent<ShieldComponent>();
            if (shieldComponent && info.attacker && victim.GetComponent<CharacterBody>().HasBuff(jackBoots))
            {
                bool canBlock = GetShieldDebuffBlock(victim, info, shieldComponent);

                if (canBlock)
                {
                    //this is gross and i don't even know if it works but i'm too tired to test it rn
                    // yeah ok it literally doesn't work, ig ive up, we'll call it a feature if no one else can fix it
                    if (info.damageType.HasFlag(DamageType.IgniteOnHit) || info.damageType.HasFlag(DamageType.PercentIgniteOnHit) || info.damageType.HasFlag(DamageType.BleedOnHit) || info.damageType.HasFlag(DamageType.ClayGoo) || info.damageType.HasFlag(DamageType.Nullify) || info.damageType.HasFlag(DamageType.SlowOnHit)) info.damageType = DamageType.Generic;

                    return;
                }
            }

            orig(self, info, victim);
        }*/

        /*private bool GetShieldDebuffBlock(GameObject self, DamageInfo info, ShieldComponent shieldComponent)
        {
            CharacterBody charB = self.GetComponent<CharacterBody>();
            Ray aimRay = shieldComponent.aimRay;
            Vector3 relativePosition = info.attacker.transform.position - aimRay.origin;
            float angle = Vector3.Angle(shieldComponent.shieldDirection, relativePosition);

            return angle < ShieldBlockAngle;
        }*/

        private void CharacterBody_Update(On.RoR2.CharacterBody.orig_Update orig, CharacterBody self) {
            if (self.name == "EnergyShield") {
                return;
            }
            orig(self);
        }
        #endregion

        private static GameObject CreateModel(GameObject main, int index)
        {
            Destroy(main.transform.Find("ModelBase").gameObject);
            Destroy(main.transform.Find("CameraPivot").gameObject);
            Destroy(main.transform.Find("AimOrigin").gameObject);

            GameObject model = null;

            if (index == 0) model = Assets.MainAssetBundle.LoadAsset<GameObject>("mdlEnforcer");
            else if (index == 1) model = Assets.MainAssetBundle.LoadAsset<GameObject>("EnforcerDisplay");

            return model;
        }

        private static void CreateDisplayPrefab()
        {
            //i know this is jank but it WORKS leave me the fuck alone :(
            GameObject tempDisplay = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody"), "EnforcerDisplay");

            GameObject model = CreateModel(tempDisplay, 1);

            GameObject gameObject = new GameObject("ModelBase");
            gameObject.transform.parent = tempDisplay.transform;
            gameObject.transform.localPosition = new Vector3(0f, -0.81f, 0f);
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);

            GameObject gameObject2 = new GameObject("CameraPivot");
            gameObject2.transform.parent = gameObject.transform;
            gameObject2.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            gameObject2.transform.localRotation = Quaternion.identity;
            gameObject2.transform.localScale = Vector3.one;

            GameObject gameObject3 = new GameObject("AimOrigin");
            gameObject3.transform.parent = gameObject.transform;
            gameObject3.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            gameObject3.transform.localRotation = Quaternion.identity;
            gameObject3.transform.localScale = Vector3.one;

            Transform transform = model.transform;
            transform.parent = gameObject.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            ModelLocator modelLocator = tempDisplay.GetComponent<ModelLocator>();
            modelLocator.modelTransform = transform;
            modelLocator.modelBaseTransform = gameObject.transform;

            ChildLocator childLocator = model.GetComponent<ChildLocator>();

            CharacterModel characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = null;
            characterModel.baseRendererInfos = new CharacterModel.RendererInfo[]
            {
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = model.GetComponentInChildren<SkinnedMeshRenderer>().material,
                    renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("ShotgunModel").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("ShotgunModel").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Shield").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Shield").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Attachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Attachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Pump").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Pump").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("RifleModel").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("RifleModel").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("EngiShield").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("EngiShield").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("RifleAttachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("RifleAttachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Blaster").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Blaster").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("BlasterAttachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("BlasterAttachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("BlasterRifle").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("BlasterRifle").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("BlasterRifleAttachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("BlasterRifleAttachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("StormtrooperHelm").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("StormtrooperHelm").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                }
            };
            characterModel.autoPopulateLightInfos = true;
            characterModel.invisibilityCount = 0;
            characterModel.temporaryOverlays = new List<TemporaryOverlay>();

            characterModel.SetFieldValue("mainSkinnedMeshRenderer", characterModel.baseRendererInfos[0].renderer.gameObject.GetComponent<SkinnedMeshRenderer>());

            characterDisplay = PrefabAPI.InstantiateClone(tempDisplay.GetComponent<ModelLocator>().modelBaseTransform.gameObject, "EnforcerDisplay", true);

            characterDisplay.AddComponent<MenuSound>();
        }

        private static void CreatePrefab()
        {
            //...what?
            // https://youtu.be/zRXl8Ow2bUs

            #region add all the things
            characterPrefab = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody"), "EnforcerBody");

            characterPrefab.GetComponent<NetworkIdentity>().localPlayerAuthority = true;

            GameObject model = CreateModel(characterPrefab, 0);

            GameObject gameObject = new GameObject("ModelBase");
            gameObject.transform.parent = characterPrefab.transform;
            gameObject.transform.localPosition = new Vector3(0f, -0.81f, 0f);
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);

            GameObject gameObject2 = new GameObject("CameraPivot");
            gameObject2.transform.parent = gameObject.transform;
            gameObject2.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            gameObject2.transform.localRotation = Quaternion.identity;
            gameObject2.transform.localScale = Vector3.one;

            GameObject gameObject3 = new GameObject("AimOrigin");
            gameObject3.transform.parent = gameObject.transform;
            gameObject3.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            gameObject3.transform.localRotation = Quaternion.identity;
            gameObject3.transform.localScale = Vector3.one;

            Transform transform = model.transform;
            transform.parent = gameObject.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            CharacterDirection characterDirection = characterPrefab.GetComponent<CharacterDirection>();
            characterDirection.moveVector = Vector3.zero;
            characterDirection.targetTransform = gameObject.transform;
            characterDirection.overrideAnimatorForwardTransform = null;
            characterDirection.rootMotionAccumulator = null;
            characterDirection.modelAnimator = model.GetComponentInChildren<Animator>();
            characterDirection.driveFromRootRotation = false;
            characterDirection.turnSpeed = 720f;

            CharacterBody bodyComponent = characterPrefab.GetComponent<CharacterBody>();
            bodyComponent.bodyIndex = -1;
            bodyComponent.name = "EnforcerBody";
            bodyComponent.baseNameToken = "ENFORCER_NAME";
            bodyComponent.subtitleNameToken = "ENFORCER_SUBTITLE";
            bodyComponent.bodyFlags = CharacterBody.BodyFlags.ImmuneToExecutes;
            bodyComponent.rootMotionInMainState = false;
            bodyComponent.mainRootSpeed = 0;
            bodyComponent.baseMaxHealth = 160;
            bodyComponent.levelMaxHealth = 48;
            bodyComponent.baseRegen = 0.5f;
            bodyComponent.levelRegen = 0.25f;
            bodyComponent.baseMaxShield = 0;
            bodyComponent.levelMaxShield = 0;
            bodyComponent.baseMoveSpeed = 7;
            bodyComponent.levelMoveSpeed = 0;
            bodyComponent.baseAcceleration = 80;
            bodyComponent.baseJumpPower = 15;
            bodyComponent.levelJumpPower = 0;
            bodyComponent.baseDamage = 12;
            bodyComponent.levelDamage = 2.4f;
            bodyComponent.baseAttackSpeed = 1;
            bodyComponent.levelAttackSpeed = 0;
            bodyComponent.baseCrit = 1;
            bodyComponent.levelCrit = 0;
            bodyComponent.baseArmor = 20;
            bodyComponent.levelArmor = 0;
            bodyComponent.baseJumpCount = 1;
            bodyComponent.sprintingSpeedMultiplier = 1.45f;
            bodyComponent.wasLucky = false;
            bodyComponent.hideCrosshair = false;
            bodyComponent.crosshairPrefab = Resources.Load<GameObject>("Prefabs/Crosshair/SMGCrosshair");
            bodyComponent.aimOriginTransform = gameObject3.transform;
            bodyComponent.hullClassification = HullClassification.Human;
            bodyComponent.portraitIcon = Assets.charPortrait;
            bodyComponent.isChampion = false;
            bodyComponent.currentVehicle = null;
            bodyComponent.skinIndex = 0U;

            LoadoutAPI.AddSkill(typeof(EnforcerMain));

            var stateMachine = bodyComponent.GetComponent<EntityStateMachine>();
            stateMachine.mainStateType = new SerializableEntityStateType(typeof(EnforcerMain));

            CharacterMotor characterMotor = characterPrefab.GetComponent<CharacterMotor>();
            characterMotor.walkSpeedPenaltyCoefficient = 1f;
            characterMotor.characterDirection = characterDirection;
            characterMotor.muteWalkMotion = false;
            characterMotor.mass = 200f;
            characterMotor.airControl = 0.25f;
            characterMotor.disableAirControlUntilCollision = false;
            characterMotor.generateParametersOnAwake = true;

            CameraTargetParams cameraTargetParams = characterPrefab.GetComponent<CameraTargetParams>();
            cameraTargetParams.cameraParams = Resources.Load<GameObject>("Prefabs/CharacterBodies/LoaderBody").GetComponent<CameraTargetParams>().cameraParams;
            cameraTargetParams.cameraPivotTransform = null;
            cameraTargetParams.aimMode = CameraTargetParams.AimType.Standard;
            cameraTargetParams.recoil = Vector2.zero;
            cameraTargetParams.idealLocalCameraPos = Vector3.zero;
            cameraTargetParams.dontRaycastToPivot = false;

            ModelLocator modelLocator = characterPrefab.GetComponent<ModelLocator>();
            modelLocator.modelTransform = transform;
            modelLocator.modelBaseTransform = gameObject.transform;

            ChildLocator childLocator = model.GetComponent<ChildLocator>();

            CharacterModel characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = bodyComponent;
            characterModel.baseRendererInfos = new CharacterModel.RendererInfo[]
            {
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = model.GetComponentInChildren<SkinnedMeshRenderer>().material,
                    renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("ShotgunModel").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("ShotgunModel").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Shield").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Shield").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Attachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Attachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Pump").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Pump").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("RifleModel").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("RifleModel").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("EngiShield").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("EngiShield").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("RifleAttachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("RifleAttachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("Blaster").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("Blaster").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("BlasterAttachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("BlasterAttachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("BlasterRifle").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("BlasterRifle").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("BlasterRifleAttachment").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("BlasterRifleAttachment").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("StormtrooperHelm").GetComponentInChildren<MeshRenderer>().material,
                    renderer = childLocator.FindChild("StormtrooperHelm").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                }
            };

            characterModel.autoPopulateLightInfos = true;
            characterModel.invisibilityCount = 0;
            characterModel.temporaryOverlays = new List<TemporaryOverlay>();

            characterModel.SetFieldValue("mainSkinnedMeshRenderer", characterModel.baseRendererInfos[0].renderer.gameObject.GetComponent<SkinnedMeshRenderer>());

            TeamComponent teamComponent = null;
            if (characterPrefab.GetComponent<TeamComponent>() != null) teamComponent = characterPrefab.GetComponent<TeamComponent>();
            else teamComponent = characterPrefab.GetComponent<TeamComponent>();
            teamComponent.hideAllyCardDisplay = false;
            teamComponent.teamIndex = TeamIndex.None;

            HealthComponent healthComponent = characterPrefab.GetComponent<HealthComponent>();
            healthComponent.health = 160f;
            healthComponent.shield = 0f;
            healthComponent.barrier = 0f;
            healthComponent.magnetiCharge = 0f;
            healthComponent.body = null;
            healthComponent.dontShowHealthbar = false;
            healthComponent.globalDeathEventChanceCoefficient = 1f;

            characterPrefab.GetComponent<Interactor>().maxInteractionDistance = 3f;
            characterPrefab.GetComponent<InteractionDriver>().highlightInteractor = true;

            CharacterDeathBehavior characterDeathBehavior = characterPrefab.GetComponent<CharacterDeathBehavior>();
            characterDeathBehavior.deathStateMachine = characterPrefab.GetComponent<EntityStateMachine>();
            //characterDeathBehavior.deathState = new SerializableEntityStateType(typeof(GenericCharacterDeath));

            SfxLocator sfxLocator = characterPrefab.GetComponent<SfxLocator>();
            sfxLocator.deathSound = Sounds.DeathSound;
            sfxLocator.barkSound = "";
            sfxLocator.openSound = "";
            sfxLocator.landingSound = "Play_char_land";
            sfxLocator.fallDamageSound = "Play_char_land_fall_damage";
            sfxLocator.aliveLoopStart = "";
            sfxLocator.aliveLoopStop = "";

            Rigidbody rigidbody = characterPrefab.GetComponent<Rigidbody>();
            rigidbody.mass = 200f;
            rigidbody.drag = 0f;
            rigidbody.angularDrag = 0f;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbody.constraints = RigidbodyConstraints.None;

            CapsuleCollider capsuleCollider = characterPrefab.GetComponent<CapsuleCollider>();
            capsuleCollider.isTrigger = false;
            capsuleCollider.material = null;
            capsuleCollider.center = new Vector3(0f, 0f, 0f);
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 1.82f;
            capsuleCollider.direction = 1;

            KinematicCharacterMotor kinematicCharacterMotor = characterPrefab.GetComponent<KinematicCharacterMotor>();
            kinematicCharacterMotor.CharacterController = characterMotor;
            kinematicCharacterMotor.Capsule = capsuleCollider;
            kinematicCharacterMotor.Rigidbody = rigidbody;

            kinematicCharacterMotor.DetectDiscreteCollisions = false;
            kinematicCharacterMotor.GroundDetectionExtraDistance = 0f;
            kinematicCharacterMotor.MaxStepHeight = 0.2f;
            kinematicCharacterMotor.MinRequiredStepDepth = 0.1f;
            kinematicCharacterMotor.MaxStableSlopeAngle = 55f;
            kinematicCharacterMotor.MaxStableDistanceFromLedge = 0.5f;
            kinematicCharacterMotor.PreventSnappingOnLedges = false;
            kinematicCharacterMotor.MaxStableDenivelationAngle = 55f;
            kinematicCharacterMotor.RigidbodyInteractionType = RigidbodyInteractionType.None;
            kinematicCharacterMotor.PreserveAttachedRigidbodyMomentum = true;
            kinematicCharacterMotor.HasPlanarConstraint = false;
            kinematicCharacterMotor.PlanarConstraintAxis = Vector3.up;
            kinematicCharacterMotor.StepHandling = StepHandlingMethod.None;
            kinematicCharacterMotor.LedgeHandling = true;
            kinematicCharacterMotor.InteractiveRigidbodyHandling = true;
            kinematicCharacterMotor.SafeMovement = false;

            HurtBoxGroup hurtBoxGroup = model.AddComponent<HurtBoxGroup>();

            if (model.transform.Find("TempHurtbox") == null) Debug.Log("no hurtbox xd");
            HurtBox mainHurtbox = model.transform.Find("TempHurtbox").GetComponent<CapsuleCollider>().gameObject.AddComponent<HurtBox>();
            mainHurtbox.gameObject.layer = LayerIndex.entityPrecise.intVal;
            mainHurtbox.healthComponent = healthComponent;
            mainHurtbox.isBullseye = true;
            mainHurtbox.damageModifier = HurtBox.DamageModifier.Normal;
            mainHurtbox.hurtBoxGroup = hurtBoxGroup;
            mainHurtbox.indexInGroup = 0;

            //make a hurtbox for the shield since this works apparently !
            HurtBox shieldHurtbox = childLocator.FindChild("ShieldHurtbox").gameObject.AddComponent<HurtBox>();
            shieldHurtbox.gameObject.layer = LayerIndex.entityPrecise.intVal;
            shieldHurtbox.healthComponent = healthComponent;
            shieldHurtbox.isBullseye = false;
            shieldHurtbox.damageModifier = HurtBox.DamageModifier.Barrier;
            shieldHurtbox.hurtBoxGroup = hurtBoxGroup;
            shieldHurtbox.indexInGroup = 1;

            hurtBoxGroup.hurtBoxes = new HurtBox[]
            {
                mainHurtbox,
                shieldHurtbox
            };

            hurtBoxGroup.mainHurtBox = mainHurtbox;
            hurtBoxGroup.bullseyeCount = 1;

            //make a hitbox for shoulder bash
            HitBoxGroup hitBoxGroup = model.AddComponent<HitBoxGroup>();

            GameObject chargeHitbox = new GameObject("ChargeHitbox");
            chargeHitbox.transform.parent = characterPrefab.transform;
            chargeHitbox.transform.localPosition = new Vector3(0f, 0f, 0f);
            chargeHitbox.transform.localRotation = Quaternion.identity;
            chargeHitbox.transform.localScale = new Vector3(8f, 8f, 8f);

            HitBox hitBox = chargeHitbox.AddComponent<HitBox>();
            chargeHitbox.layer = LayerIndex.projectile.intVal;

            hitBoxGroup.hitBoxes = new HitBox[]
            {
                hitBox
            };

            hitBoxGroup.groupName = "Charge";

            FootstepHandler footstepHandler = model.AddComponent<FootstepHandler>();
            footstepHandler.baseFootstepString = "Play_player_footstep";
            footstepHandler.sprintFootstepOverrideString = "";
            footstepHandler.enableFootstepDust = true;
            footstepHandler.footstepDustPrefab = Resources.Load<GameObject>("Prefabs/GenericFootstepDust");

            RagdollController ragdollController = model.GetComponent<RagdollController>();

            PhysicMaterial physicMat = Resources.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody").GetComponentInChildren<RagdollController>().bones[1].GetComponent<Collider>().material;

            foreach (Transform i in ragdollController.bones)
            {
                if (i)
                {
                    i.gameObject.layer = LayerIndex.ragdoll.intVal;
                    Collider j = i.GetComponent<Collider>();
                    if (j)
                    {
                        j.material = physicMat;
                        j.sharedMaterial = physicMat;
                    }
                }
            }

            AimAnimator aimAnimator = model.AddComponent<AimAnimator>();
            aimAnimator.directionComponent = characterDirection;
            aimAnimator.pitchRangeMax = 60f;
            aimAnimator.pitchRangeMin = -60f;
            aimAnimator.yawRangeMin = -90f;
            aimAnimator.yawRangeMax = 90f;
            aimAnimator.pitchGiveupRange = 30f;
            aimAnimator.yawGiveupRange = 10f;
            aimAnimator.giveupDuration = 3f;
            aimAnimator.inputBank = characterPrefab.GetComponent<InputBankTest>();

            characterPrefab.AddComponent<ShieldComponent>();
            characterPrefab.AddComponent<EnforcerWeaponComponent>();
            characterPrefab.AddComponent<EnforcerLightController>();

            #endregion
        }

        private void RegisterCharacter()
        {
            string desc = "The Enforcer is a defensive juggernaut who can give and take a beating.<color=#CCD3E0>" + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Riot Shotgun can pierce through many enemies at once." + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Batting away enemies with Shield Bash guarantees you will keep enemies at a safe range." + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Use Tear Gas to weaken large crowds of enemies, then get in close and crush them." + Environment.NewLine + Environment.NewLine;
            desc = desc + "< ! > Make sure to use Protect and Serve against walls to prevent enemies from flanking you." + Environment.NewLine + Environment.NewLine;

            LanguageAPI.Add("ENFORCER_NAME", "Enforcer");
            LanguageAPI.Add("ENFORCER_DESCRIPTION", desc);
            LanguageAPI.Add("ENFORCER_SUBTITLE", "Mutated Beyond Recognition");
            //LanguageAPI.Add("ENFORCER_LORE", "I'M FUCKING INVINCIBLE");
            LanguageAPI.Add("ENFORCER_LORE", "\n<style=cMono>\"You don't have to do this.\"</style>\r\n\r\nThe words echoed in his head, but yet he continued. The pod was only five feet away, he had a chance to leave, but yet something in his core kept him moving. It was unknown what kept him moving - even to him, but he didn't question it. The same thing kept him moving was the same thing that made him step when he had been given orders. To him, it was natural, but this time it didn't seem that way.");
            LanguageAPI.Add("ENFORCER_OUTRO_FLAVOR", "..and so he left, mutated beyond recognition.");

            characterDisplay.AddComponent<NetworkIdentity>();

            SurvivorDef survivorDef = new SurvivorDef
            {
                name = "ENFORCER_NAME",
                unlockableName = "",
                descriptionToken = "ENFORCER_DESCRIPTION",
                primaryColor = characterColor,
                bodyPrefab = characterPrefab,
                displayPrefab = characterDisplay
            };


            SurvivorAPI.AddSurvivor(survivorDef);

            SkillSetup();

            BodyCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(characterPrefab);
            };

            characterPrefab.tag = "SkinReady";
        }

        private void RegisterBuffs() {
            BuffDef jackBootsDef = new BuffDef {
                name = "Heavyweight",
                iconPath = "Textures/BuffIcons/texBuffGenericShield",//i'll add this soon
                buffColor = characterColor,
                canStack = false,
                isDebuff = false,
                eliteIndex = EliteIndex.None
            };
            CustomBuff jackBoots = new CustomBuff(jackBootsDef);
            EnforcerPlugin.jackBoots = BuffAPI.Add(jackBoots);

            BuffDef energyShieldBuffDef = new BuffDef
            {
                name = "Heavyweight",
                iconPath = "Textures/BuffIcons/texBuffGenericShield",
                buffColor = characterColor,
                canStack = false,
                isDebuff = false,
                eliteIndex = EliteIndex.None
            };
            CustomBuff energyShieldBuff = new CustomBuff(energyShieldBuffDef);
            EnforcerPlugin.energyShieldBuff = BuffAPI.Add(energyShieldBuff);

            BuffDef tearGasDef = new BuffDef
            {
                name = "TearGasDebuff",
                iconPath = "Textures/BuffIcons/texBuffCloakIcon",
                buffColor = Color.grey,
                canStack = false,
                isDebuff = true,
                eliteIndex = EliteIndex.None
            };
            CustomBuff tearGas = new CustomBuff(tearGasDef);
            EnforcerPlugin.tearGasDebuff = BuffAPI.Add(tearGas);
        }

        private void RegisterProjectile()
        {
            //i'm the treasure, baby, i'm the prize

            stunGrenade = Resources.Load<GameObject>("Prefabs/Projectiles/CommandoGrenadeProjectile").InstantiateClone("EnforcerStunGrenade", true);

            ProjectileController stunGrenadeController = stunGrenade.GetComponent<ProjectileController>();
            ProjectileImpactExplosion stunGrenadeImpact = stunGrenade.GetComponent<ProjectileImpactExplosion>();
            ProjectileSimple stunGrenadeSimple = stunGrenade.GetComponent<ProjectileSimple>();

            GameObject stunGrenadeModel = Assets.stunGrenadeModel.InstantiateClone("StunGrenadeGhost", true);
            stunGrenadeModel.AddComponent<NetworkIdentity>();
            stunGrenadeModel.AddComponent<ProjectileGhostController>();

            stunGrenadeController.ghostPrefab = stunGrenadeModel;

            stunGrenadeImpact.lifetimeExpiredSoundString = "";
            stunGrenadeImpact.explosionSoundString = Sounds.StunExplosion;
            stunGrenadeImpact.offsetForLifetimeExpiredSound = 1;
            stunGrenadeImpact.destroyOnEnemy = false;
            stunGrenadeImpact.destroyOnWorld = false;
            stunGrenadeImpact.timerAfterImpact = true;
            stunGrenadeImpact.falloffModel = BlastAttack.FalloffModel.Linear;
            stunGrenadeImpact.lifetimeAfterImpact = 0f;
            stunGrenadeImpact.lifetimeRandomOffset = 0;
            stunGrenadeImpact.blastRadius = 8;
            stunGrenadeImpact.blastDamageCoefficient = 1;
            stunGrenadeImpact.blastProcCoefficient = 0.6f;
            stunGrenadeImpact.fireChildren = false;
            stunGrenadeImpact.childrenCount = 0;
            stunGrenadeController.procCoefficient = 1;

            projectilePrefab = Resources.Load<GameObject>("Prefabs/Projectiles/CommandoGrenadeProjectile").InstantiateClone("EnforcerTearGasGrenade", true);
            tearGasPrefab = Resources.Load<GameObject>("Prefabs/Projectiles/SporeGrenadeProjectileDotZone").InstantiateClone("TearGasDotZone", true);

            ProjectileController grenadeController = projectilePrefab.GetComponent<ProjectileController>();
            ProjectileController tearGasController = tearGasPrefab.GetComponent<ProjectileController>();

            ProjectileDamage grenadeDamage = projectilePrefab.GetComponent<ProjectileDamage>();
            ProjectileDamage tearGasDamage = tearGasPrefab.GetComponent<ProjectileDamage>();

            ProjectileSimple simple = projectilePrefab.GetComponent<ProjectileSimple>();

            TeamFilter filter = tearGasPrefab.GetComponent<TeamFilter>();

            ProjectileImpactExplosion grenadeImpact = projectilePrefab.GetComponent<ProjectileImpactExplosion>();
            
            Destroy(tearGasPrefab.GetComponent<ProjectileDotZone>());

            BuffWard buffWard = tearGasPrefab.AddComponent<BuffWard>();

            filter.teamIndex = TeamIndex.Player;

            GameObject grenadeModel = Assets.tearGasGrenadeModel.InstantiateClone("TearGasGhost", true);
            grenadeModel.AddComponent<NetworkIdentity>();
            grenadeModel.AddComponent<ProjectileGhostController>();

            grenadeController.ghostPrefab = grenadeModel;
            tearGasController.ghostPrefab = Assets.tearGasEffectPrefab;

            grenadeImpact.lifetimeExpiredSoundString = "";
            grenadeImpact.explosionSoundString = Sounds.GasExplosion;
            grenadeImpact.offsetForLifetimeExpiredSound = 1;
            grenadeImpact.destroyOnEnemy = false;
            grenadeImpact.destroyOnWorld = false;
            grenadeImpact.timerAfterImpact = true;
            grenadeImpact.falloffModel = BlastAttack.FalloffModel.SweetSpot;
            grenadeImpact.lifetime = 18;
            grenadeImpact.lifetimeAfterImpact = 0.5f;
            grenadeImpact.lifetimeRandomOffset = 0;
            grenadeImpact.blastRadius = 6;
            grenadeImpact.blastDamageCoefficient = 1;
            grenadeImpact.blastProcCoefficient = 1;
            grenadeImpact.fireChildren = true;
            grenadeImpact.childrenCount = 1;
            grenadeImpact.childrenProjectilePrefab = tearGasPrefab;
            grenadeImpact.childrenDamageCoefficient = 0;
            grenadeImpact.impactEffect = null;


            grenadeController.procCoefficient = 1;
            tearGasController.procCoefficient = 0;

            grenadeDamage.crit = false;
            grenadeDamage.damage = 0f;
            grenadeDamage.damageColorIndex = DamageColorIndex.Default;
            grenadeDamage.damageType = DamageType.Stun1s;
            grenadeDamage.force = 0;

            tearGasDamage.crit = false;
            tearGasDamage.damage = 0;
            tearGasDamage.damageColorIndex = DamageColorIndex.WeakPoint;
            tearGasDamage.damageType = DamageType.Stun1s;
            tearGasDamage.force = -1000;

            buffWard.radius = 20;
            buffWard.interval = 1;
            buffWard.rangeIndicator = null;
            buffWard.buffType = tearGasDebuff;
            buffWard.buffDuration = 1.5f;
            buffWard.floorWard = true;
            buffWard.expires = false;
            buffWard.invertTeamFilter = true;
            buffWard.expireDuration = 0;
            buffWard.animateRadius = false;

            //this is weird but it works

            Destroy(tearGasPrefab.transform.GetChild(0).gameObject);
            GameObject gasFX = Assets.tearGasEffectPrefab.InstantiateClone("FX", true);
            gasFX.AddComponent<NetworkIdentity>();
            gasFX.AddComponent<TearGasSound>();
            gasFX.transform.parent = tearGasPrefab.transform;
            gasFX.transform.localPosition = Vector3.zero;

            //i have this really big cut on my shin and it's bleeding but i'm gonna code instead of doing something about it
            // that's the spirit, champ

            tearGasPrefab.AddComponent<DestroyOnTimer>().duration = 18;


            bulletTracer = Resources.Load<GameObject>("Prefabs/Effects/Tracers/TracerCommandoShotgun").InstantiateClone("EnforcerBulletTracer", true);

            if (!bulletTracer.GetComponent<EffectComponent>()) bulletTracer.AddComponent<EffectComponent>();
            if (!bulletTracer.GetComponent<VFXAttributes>()) bulletTracer.AddComponent<VFXAttributes>();
            if (!bulletTracer.GetComponent<NetworkIdentity>()) bulletTracer.AddComponent<NetworkIdentity>();

            foreach (LineRenderer i in bulletTracer.GetComponentsInChildren<LineRenderer>())
            {
                if (i)
                {
                    Material material = UnityEngine.Object.Instantiate<Material>(i.material);
                    material.SetColor("_TintColor", Color.yellow);
                    i.material = material;
                    i.startColor = new Color(0.88f, 0.78f, 0.25f);
                    i.endColor = new Color(0.88f, 0.78f, 0.25f);
                }
            }

            laserTracer = Resources.Load<GameObject>("Prefabs/Effects/Tracers/TracerCommandoShotgun").InstantiateClone("EnforcerLaserTracer", true);

            if (!laserTracer.GetComponent<EffectComponent>()) laserTracer.AddComponent<EffectComponent>();
            if (!laserTracer.GetComponent<VFXAttributes>()) laserTracer.AddComponent<VFXAttributes>();
            if (!laserTracer.GetComponent<NetworkIdentity>()) laserTracer.AddComponent<NetworkIdentity>();

            foreach (LineRenderer i in laserTracer.GetComponentsInChildren<LineRenderer>())
            {
                if (i)
                {
                    Material material = UnityEngine.Object.Instantiate<Material>(i.material);
                    material.SetColor("_TintColor", Color.red);
                    i.material = material;
                    i.startColor = new Color(0.8f, 0.19f, 0.19f);
                    i.endColor = new Color(0.8f, 0.19f, 0.19f);
                }
            }

            blockEffectPrefab = Resources.Load<GameObject>("Prefabs/Effects/BearProc").InstantiateClone("EnforcerBlockEffect", true);

            if (blockEffectPrefab.GetComponent<AkEvent>()) Destroy(blockEffectPrefab.GetComponent<AkEvent>());
            if (blockEffectPrefab.GetComponent<AkGameObj>()) Destroy(blockEffectPrefab.GetComponent<AkGameObj>());
            blockEffectPrefab.GetComponent<EffectComponent>().soundName = "";
            if (!blockEffectPrefab.GetComponent<NetworkIdentity>()) blockEffectPrefab.AddComponent<NetworkIdentity>();

            ProjectileCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(projectilePrefab);
                list.Add(tearGasPrefab);
                list.Add(stunGrenade);
            };

            EffectAPI.AddEffect(bulletTracer);
            EffectAPI.AddEffect(laserTracer);
            EffectAPI.AddEffect(blockEffectPrefab);
        }


        private void CreateDoppelganger()
        {
            // commando ai for now
            doppelganger = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterMasters/CommandoMonsterMaster"), "EnforcerMonsterMaster");

            MasterCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(doppelganger);
            };

            CharacterMaster component = doppelganger.GetComponent<CharacterMaster>();
            component.bodyPrefab = characterPrefab;
        }

        //add modifiers to your voids please 
        // no go fuck yourself :^)
        // suck my dick 

        private void SkillSetup()
        {
            foreach (GenericSkill obj in characterPrefab.GetComponentsInChildren<GenericSkill>())
            {
                BaseUnityPlugin.DestroyImmediate(obj);
            }

            skillLocator = characterPrefab.GetComponent<SkillLocator>();

            PrimarySetup();
            SecondarySetup();
            UtilitySetup();
            SpecialSetup();
            //AltSpecialSetup();
        }
        
        private void PrimarySetup()
        {
            LoadoutAPI.AddSkill(typeof(RiotShotgun));

            string desc = "Fire a short range <style=cIsUtility>piercing blast</style> for <style=cIsDamage>" + RiotShotgun.projectileCount + "x" + 100f * RiotShotgun.damageCoefficient + "% damage.";

            LanguageAPI.Add("ENFORCER_PRIMARY_SHOTGUN_NAME", "Riot Shotgun");
            LanguageAPI.Add("ENFORCER_PRIMARY_SHOTGUN_DESCRIPTION", desc);

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(RiotShotgun));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 0f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Any;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon1;
            mySkillDef.skillDescriptionToken = "ENFORCER_PRIMARY_SHOTGUN_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_PRIMARY_SHOTGUN_NAME";
            mySkillDef.skillNameToken = "ENFORCER_PRIMARY_SHOTGUN_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            skillLocator.primary = characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            skillLocator.primary.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = skillLocator.primary.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };

            LoadoutAPI.AddSkill(typeof(AssaultRifleState));
            LoadoutAPI.AddSkill(typeof(FireAssaultRifle));
            LoadoutAPI.AddSkill(typeof(AssaultRifleExit));

            desc = "Rapidly fire bullets dealing <style=cIsDamage>" + 100f * FireAssaultRifle.damageCoefficient + "% damage.";

            LanguageAPI.Add("ENFORCER_PRIMARY_RIFLE_NAME", "Assault Rifle");
            LanguageAPI.Add("ENFORCER_PRIMARY_RIFLE_DESCRIPTION", desc);

            mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(FireAssaultRifle));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 0f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Any;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;    
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon1;
            mySkillDef.skillDescriptionToken = "ENFORCER_PRIMARY_RIFLE_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_PRIMARY_RIFLE_NAME";
            mySkillDef.skillNameToken = "ENFORCER_PRIMARY_RIFLE_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };

            LoadoutAPI.AddSkill(typeof(SuperShotgun));

            desc = "Fire a short range <style=cIsUtility>blast</style> for <style=cIsDamage>" + RiotShotgun.projectileCount + "x" + 100f * SuperShotgun.damageCoefficient + "% damage. Has harsh damage falloff.";

            LanguageAPI.Add("ENFORCER_PRIMARY_SUPERSHOTGUN_NAME", "Super Shotgun");
            LanguageAPI.Add("ENFORCER_PRIMARY_SUPERSHOTGUN_DESCRIPTION", desc);

            mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(SuperShotgun));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 0f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Any;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon1;
            mySkillDef.skillDescriptionToken = "ENFORCER_PRIMARY_SUPERSHOTGUN_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_PRIMARY_SUPERSHOTGUN_NAME";
            mySkillDef.skillNameToken = "ENFORCER_PRIMARY_SUPERSHOTGUN_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
        }

        private void SecondarySetup()
        {
            LoadoutAPI.AddSkill(typeof(ShieldBash));
            LoadoutAPI.AddSkill(typeof(ShoulderBash));
            LoadoutAPI.AddSkill(typeof(ShoulderBashImpact));

            string desc = "Smash nearby enemies for <style=cIsDamage>" + 100f * ShieldBash.damageCoefficient + "% damage, stunning</style> and <style=cIsUtility>knocking them back</style>. Use while sprinting to perform a <style=cIsDamage>shoulder bash</style> instead. <style=cIsUtility>Deflects projectiles.</style>";

            LanguageAPI.Add("ENFORCER_SECONDARY_BASH_NAME", "Shield Bash");
            LanguageAPI.Add("ENFORCER_SECONDARY_BASH_DESCRIPTION", desc);

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(ShieldBash));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 6f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Skill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = false;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon2;
            mySkillDef.skillDescriptionToken = "ENFORCER_SECONDARY_BASH_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_SECONDARY_BASH_NAME";
            mySkillDef.skillNameToken = "ENFORCER_SECONDARY_BASH_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            skillLocator.secondary = characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            skillLocator.secondary.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = skillLocator.secondary.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
        }

        private void UtilitySetup()
        {
            LoadoutAPI.AddSkill(typeof(TearGas));

            LanguageAPI.Add("ENFORCER_UTILITY_TEARGAS_NAME", "Tear Gas");
            LanguageAPI.Add("ENFORCER_UTILITY_TEARGAS_DESCRIPTION", "Throw a grenade that explodes into <style=cIsUtility>tear gas</style> that <style=cIsDamage>heavily debilitates enemies</style> and lasts for 16 seconds.");

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(TearGas));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 24;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Skill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon3;
            mySkillDef.skillDescriptionToken = "ENFORCER_UTILITY_TEARGAS_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_UTILITY_TEARGAS_NAME";
            mySkillDef.skillNameToken = "ENFORCER_UTILITY_TEARGAS_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            skillLocator.utility = characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            skillLocator.utility.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = skillLocator.utility.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };

            LoadoutAPI.AddSkill(typeof(StunGrenade));

            LanguageAPI.Add("ENFORCER_UTILITY_STUNGRENADE_NAME", "Stun Grenade");
            LanguageAPI.Add("ENFORCER_UTILITY_STUNGRENADE_DESCRIPTION", "Launch a stun grenade, dealing <style=cIsDamage>" + 100f * StunGrenade.damageCoefficient + "% damage</style> and <style=cIsUtility>stunning</style>. <style=cIsUtility>Store up to 6 grenades</style>.");

            mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(StunGrenade));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 6;
            mySkillDef.baseRechargeInterval = 8f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Skill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon3B;
            mySkillDef.skillDescriptionToken = "ENFORCER_UTILITY_STUNGRENADE_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_UTILITY_STUNGRENADE_NAME";
            mySkillDef.skillNameToken = "ENFORCER_UTILITY_STUNGRENADE_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };
        }

        private void SpecialSetup()
        {
            LoadoutAPI.AddSkill(typeof(ProtectAndServe));

            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDUP_NAME", "Protect and Serve");
            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDUP_DESCRIPTION", "Take a defensive stance, <style=cIsUtility>blocking all damage from the front</style>. <style=cIsDamage>Increases your rate of fire</style>, but prevents sprinting and jumping.");

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(ProtectAndServe));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 0f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = true;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon4;
            mySkillDef.skillDescriptionToken = "ENFORCER_SPECIAL_SHIELDUP_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_SPECIAL_SHIELDUP_NAME";
            mySkillDef.skillNameToken = "ENFORCER_SPECIAL_SHIELDUP_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            skillLocator.special = characterPrefab.AddComponent<GenericSkill>();
            SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
            newFamily.variants = new SkillFamily.Variant[1];
            LoadoutAPI.AddSkillFamily(newFamily);
            skillLocator.special.SetFieldValue("_skillFamily", newFamily);
            SkillFamily skillFamily = skillLocator.special.skillFamily;

            skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };

            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDDOWN_NAME", "Protect and Serve");
            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDDOWN_DESCRIPTION", "Take a defensive stance, <style=cIsUtility>blocking all damage from the front</style>. <style=cIsDamage>Increases your rate of fire</style>, but prevents sprinting and jumping.");

            SkillDef mySkillDef2 = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef2.activationState = new SerializableEntityStateType(typeof(ProtectAndServe));
            mySkillDef2.activationStateMachineName = "Weapon";
            mySkillDef2.baseMaxStock = 1;
            mySkillDef2.baseRechargeInterval = 0f;
            mySkillDef2.beginSkillCooldownOnSkillEnd = false;
            mySkillDef2.canceledFromSprinting = false;
            mySkillDef2.fullRestockOnAssign = true;
            mySkillDef2.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef2.isBullets = false;
            mySkillDef2.isCombatSkill = true;
            mySkillDef2.mustKeyPress = true;
            mySkillDef2.noSprint = false;
            mySkillDef2.rechargeStock = 1;
            mySkillDef2.requiredStock = 1;
            mySkillDef2.shootDelay = 0f;
            mySkillDef2.stockToConsume = 1;
            mySkillDef2.icon = Assets.icon4B;
            mySkillDef2.skillDescriptionToken = "ENFORCER_SPECIAL_SHIELDDOWN_DESCRIPTION";
            mySkillDef2.skillName = "ENFORCER_SPECIAL_SHIELDDOWN_NAME";
            mySkillDef2.skillNameToken = "ENFORCER_SPECIAL_SHIELDDOWN_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef2);

            shieldDownDef = mySkillDef;
            shieldUpDef = mySkillDef2;
        }

        private void AltSpecialSetup()
        {
            LoadoutAPI.AddSkill(typeof(EnergyShield));

            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDON_NAME", "Project and Swerve");
            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDON_DESCRIPTION", "Take a defensive stance, <style=cIsUtility>projecting an Energy Shield in front of you</style>. <style=cIsDamage>Increases your rate of fire</style>, but prevents sprinting and jumping.");

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(EnergyShield));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 0f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef.isBullets = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = true;
            mySkillDef.noSprint = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.shootDelay = 0f;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon3;
            mySkillDef.skillDescriptionToken = "ENFORCER_SPECIAL_SHIELDON_DESCRIPTION";
            mySkillDef.skillName = "ENFORCER_SPECIAL_SHIELDON_NAME";
            mySkillDef.skillNameToken = "ENFORCER_SPECIAL_SHIELDON_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef);

            SkillLocator skillLocator = characterPrefab.GetComponent<SkillLocator>();
            SkillFamily skillFamily = skillLocator.special.skillFamily;

            Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
            skillFamily.variants[1] = new SkillFamily.Variant
            {
                skillDef = mySkillDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
            };

            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDOFF_NAME", "Project and Swerve");
            LanguageAPI.Add("ENFORCER_SPECIAL_SHIELDOFF_DESCRIPTION", "Take a defensive stance, <style=cIsUtility>projecting an Energy Shield in front of you</style>. <style=cIsDamage>Increases your rate of fire</style>, but prevents sprinting and jumping.");

            SkillDef mySkillDef2 = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef2.activationState = new SerializableEntityStateType(typeof(EnergyShield));
            mySkillDef2.activationStateMachineName = "Weapon";
            mySkillDef2.baseMaxStock = 1;
            mySkillDef2.baseRechargeInterval = 0f;
            mySkillDef2.beginSkillCooldownOnSkillEnd = false;
            mySkillDef2.canceledFromSprinting = false;
            mySkillDef2.fullRestockOnAssign = true;
            mySkillDef2.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef2.isBullets = false;
            mySkillDef2.isCombatSkill = true;
            mySkillDef2.mustKeyPress = true;
            mySkillDef2.noSprint = false;
            mySkillDef2.rechargeStock = 1;
            mySkillDef2.requiredStock = 1;
            mySkillDef2.shootDelay = 0f;
            mySkillDef2.stockToConsume = 1;
            mySkillDef2.icon = Assets.icon3B;
            mySkillDef2.skillDescriptionToken = "ENFORCER_SPECIAL_SHIELDOFF_DESCRIPTION";
            mySkillDef2.skillName = "ENFORCER_SPECIAL_SHIELDOFF_NAME";
            mySkillDef2.skillNameToken = "ENFORCER_SPECIAL_SHIELDOFF_NAME";

            LoadoutAPI.AddSkillDef(mySkillDef2);

            shieldOffDef = mySkillDef;
            shieldOnDef = mySkillDef2;
        }
    }

    public class MenuSound : MonoBehaviour
    {
        private void OnEnable()
        {
            Util.PlaySound(Sounds.SirenSpawn, base.gameObject);
        }
    }

    public class TearGasSound : MonoBehaviour
    {
        private uint playID;

        private void Awake()
        {
            playID = Util.PlaySound(Sounds.GasContinuous, base.gameObject);
        }

        private void OnDestroy()
        {
            AkSoundEngine.StopPlayingID(playID);
        }
    }

    public static class Assets
    {
        public static AssetBundle MainAssetBundle = null;

        public static AssetBundle TempAssetBundle = null;

        public static Texture charPortrait;

        //public static Sprite iconP;
        public static Sprite icon1;//shotgun
        public static Sprite icon2;//shield bash
        public static Sprite icon3;//tear gas
        public static Sprite icon3B;//stun grenade
        public static Sprite icon4;//protect and serve
        public static Sprite icon4B;//protect and serve cancel

        public static GameObject grenade;

        public static GameObject tearGasGrenadeModel;
        public static GameObject tearGasEffectPrefab;

        public static GameObject stunGrenadeModel;

        public static Mesh stormtrooperMesh;
        public static Mesh engiMesh;

        public static void PopulateAssets()
        {
            if (MainAssetBundle == null)
            {
                using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Enforcer.enforcer"))
                {
                    MainAssetBundle = AssetBundle.LoadFromStream(assetStream);
                }
            }

            //fuck whoever wrote this code and fuck you
            // comment out the soundbank shit and then wonder why sounds aren't working you're literally fucking retarded holy hell
            using(Stream manifestResourceStream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("Enforcer.EnforcerBank.bnk"))
            {
                byte[] array = new byte[manifestResourceStream2.Length];
                manifestResourceStream2.Read(array, 0, array.Length);
                SoundAPI.SoundBanks.Add(array);
            }

            charPortrait = MainAssetBundle.LoadAsset<Sprite>("EnforcerBody").texture;

            //iconP = MainAssetBundle.LoadAsset<Sprite>("PassiveIcon");
            icon1 = MainAssetBundle.LoadAsset<Sprite>("Skill1Icon");
            icon2 = MainAssetBundle.LoadAsset<Sprite>("Skill2Icon");
            icon3 = MainAssetBundle.LoadAsset<Sprite>("Skill3Icon");
            icon3B = MainAssetBundle.LoadAsset<Sprite>("Skill3BIcon");
            icon4 = MainAssetBundle.LoadAsset<Sprite>("Skill4Icon");
            icon4B = MainAssetBundle.LoadAsset<Sprite>("Skill4BIcon");

            //grenade = TempAssetBundle.LoadAsset<GameObject>("Grenade");
            tearGasGrenadeModel = MainAssetBundle.LoadAsset<GameObject>("TearGasGrenade");
            tearGasEffectPrefab = MainAssetBundle.LoadAsset<GameObject>("TearGasEffect");

            stunGrenadeModel = MainAssetBundle.LoadAsset<GameObject>("StunGrenade");

            //add vfx shit so nothing breaks
            //tearGasEffectPrefab.AddComponent<VFXAttributes>().vfxPriority = VFXAttributes.VFXPriority.Always;
            //tearGasEffectPrefab.AddComponent<EffectComponent>().applyScale = false;
            //actually this wasn't even needed

            stormtrooperMesh = MainAssetBundle.LoadAsset<Mesh>("StormtrooperMesh");
            engiMesh = MainAssetBundle.LoadAsset<Mesh>("EngiforcerMesh");
        }
    }

    public static class Sounds
    {
        public static readonly string FireShotgun = "Shotgun_shot";
        public static readonly string FireShotgunCrit = "Shotgun_shot_crit";
        public static readonly string FireClassicShotgun = "Ror1_Shotgun";

        public static readonly string FireAssaultRifleSlow = "Assault_Shots_1";
        public static readonly string FireAssaultRifleFast = "Assault_Shots_2";

        public static readonly string FireBlasterShotgun = "Blaster_Shotgun";
        public static readonly string FireBlasterRifle = "Blaster_Rifle";

        public static readonly string ShieldBash = "Bash";
        public static readonly string BashHitEnemy = "Bash_Hit_Enemy";
        public static readonly string BashDeflect = "Bash_Deflect";

        public static readonly string ShoulderBashHit = "Shoulder_Bash_Hit";

        public static readonly string LaunchStunGrenade = "Launch_Stun";
        public static readonly string StunExplosion = "Stun_Explosion";

        public static readonly string LaunchTearGas = "Launch_Gas";
        public static readonly string GasExplosion = "Gas_Explosion";
        public static readonly string GasContinuous = "Gas_Continous";

        public static readonly string ShieldUp = "R_up";
        public static readonly string ShieldDown = "R_down";

        public static readonly string ShieldBlockLight = "Shield_Block_light";
        public static readonly string ShieldBlockHeavy = "Shield_Block_heavy";

        public static readonly string EnergyShieldUp = "Energy_R_Up";
        public static readonly string EnergyShieldDown = "Energy_R_down";

        public static readonly string ShellHittingFloor = "Shell_Hitting_floor";

        public static readonly string DeathSound = "Death_Siren";
        public static readonly string SirenButton = "Siren_Button";
        public static readonly string SirenSpawn = "Siren_Spawn";

        public static readonly string DefaultDance = "Default_forcer";
        public static readonly string DOOM = "DOOM";
    }
}