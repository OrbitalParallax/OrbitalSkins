using BepInEx;
using BepInEx.Logging;
using RoR2;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using System.Security.Permissions;
using MonoMod.RuntimeDetour.HookGen;
using RoR2.ContentManagement;
using UnityEngine.AddressableAssets;


#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
namespace SurvivorsReturn
{
    
    [BepInPlugin("com.Orbital.SurvivorsReturn","SurvivorsReturn","1.1.0")]
    public partial class SurvivorsReturnPlugin : BaseUnityPlugin
    {
        internal static SurvivorsReturnPlugin Instance { get; private set; }
        internal static ManualLogSource InstanceLogger => Instance?.Logger;
        
        private static AssetBundle assetBundle;
        private static readonly List<Material> materialsWithRoRShader = new List<Material>();
        private void Start()
        {
            Instance = this;

            BeforeStart();

            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SurvivorsReturn.orbitalsurvivorsreturn"))
            {
                assetBundle = AssetBundle.LoadFromStream(assetStream);
            }

            BodyCatalog.availability.CallWhenAvailable(BodyCatalogInit);
            HookEndpointManager.Add(typeof(Language).GetMethod(nameof(Language.LoadStrings)), (Action<Action<Language>, Language>)LanguageLoadStrings);

            ReplaceShaders();

            AfterStart();
        }

        partial void BeforeStart();
        partial void AfterStart();
        static partial void BeforeBodyCatalogInit();
        static partial void AfterBodyCatalogInit();

        private static void ReplaceShaders()
        {
            LoadMaterialsWithReplacedShader(@"RoR2/Base/Shaders/HGStandard.shader"
                ,@"Assets/Resources/MANDO/SurvivorMat.mat"                ,@"Assets/Resources/HNTRS/borb.mat"                ,@"Assets/Resources/HNTRS/huntgr.mat");
        }

        private static void LoadMaterialsWithReplacedShader(string shaderPath, params string[] materialPaths)
        {
            var shader = Addressables.LoadAssetAsync<Shader>(shaderPath).WaitForCompletion();
            foreach (var materialPath in materialPaths)
            {
                var material = assetBundle.LoadAsset<Material>(materialPath);
                material.shader = shader;
                materialsWithRoRShader.Add(material);
            }
        }

        private static void LanguageLoadStrings(Action<Language> orig, Language self)
        {
            orig(self);

            self.SetStringByToken("ORBITAL_SKIN_COMMANDO_NAME", "MK II");
            self.SetStringByToken("ORBITAL_SKIN_HUNTRESS_NAME", "Urban");
        }

        private static void Nothing(Action<SkinDef> orig, SkinDef self)
        {

        }

        private static void BodyCatalogInit()
        {
            BeforeBodyCatalogInit();

            var awake = typeof(SkinDef).GetMethod(nameof(SkinDef.Awake), BindingFlags.NonPublic | BindingFlags.Instance);
            HookEndpointManager.Add(awake, (Action<Action<SkinDef>, SkinDef>)Nothing);

            AddCommandoBodyCommandoSkin();
            AddHuntressBodyHuntressSkin();
            
            HookEndpointManager.Remove(awake, (Action<Action<SkinDef>, SkinDef>)Nothing);

            AfterBodyCatalogInit();
        }

        static partial void CommandoBodyCommandoSkinAdded(SkinDef skinDef, GameObject bodyPrefab);

        private static void AddCommandoBodyCommandoSkin()
        {
            var bodyName = "CommandoBody";
            var skinName = "Commando";
            try
            {
                var bodyPrefab = BodyCatalog.FindBodyPrefab(bodyName);
                if (!bodyPrefab)
                {
                    InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin because \"{bodyName}\" doesn't exist");
                    return;
                }

                var modelLocator = bodyPrefab.GetComponent<ModelLocator>();
                if (!modelLocator)
                {
                    InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\" because it doesn't have \"ModelLocator\" component");
                    return;
                }

                var mdl = modelLocator.modelTransform.gameObject;
                var skinController = mdl ? mdl.GetComponent<ModelSkinController>() : null;
                if (!skinController)
                {
                    InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\" because it doesn't have \"ModelSkinController\" component");
                    return;
                }

                var renderers = mdl.GetComponentsInChildren<Renderer>(true);

                var skin = ScriptableObject.CreateInstance<SkinDef>();
                TryCatchThrow("Icon", () =>
                {
                    skin.icon = assetBundle.LoadAsset<Sprite>(@"Assets\SkinMods\SurvivorsReturn\Icons\CommandoIcon.png");
                });
                skin.name = skinName;
                skin.nameToken = "ORBITAL_SKIN_COMMANDO_NAME";
                skin.rootObject = mdl;
                TryCatchThrow("Base Skins", () =>
                {
                    skin.baseSkins = new SkinDef[] 
                    { 
                        skinController.skins[0],
                    };
                });
                TryCatchThrow("Unlockable Name", () =>
                {
                    skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault(def => def.cachedName == "Return");
                });
                TryCatchThrow("Game Object Activations", () =>
                {
                    skin.gameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
                });
                TryCatchThrow("Renderer Infos", () =>
                {
                    skin.rendererInfos = new CharacterModel.RendererInfo[]
                    {
                        new CharacterModel.RendererInfo
                        {
                            defaultMaterial = assetBundle.LoadAsset<Material>(@"Assets/Resources/MANDO/SurvivorMat.mat"),
                            defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                            ignoreOverlays = false,
                            renderer = renderers[0]
                        },
                        new CharacterModel.RendererInfo
                        {
                            defaultMaterial = assetBundle.LoadAsset<Material>(@"Assets/Resources/MANDO/SurvivorMat.mat"),
                            defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                            ignoreOverlays = false,
                            renderer = renderers[3]
                        },
                        new CharacterModel.RendererInfo
                        {
                            defaultMaterial = assetBundle.LoadAsset<Material>(@"Assets/Resources/MANDO/SurvivorMat.mat"),
                            defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                            ignoreOverlays = false,
                            renderer = renderers[6]
                        },
                    };
                });
                TryCatchThrow("Mesh Replacements", () =>
                {
                    skin.meshReplacements = new SkinDef.MeshReplacement[]
                    {
                        new SkinDef.MeshReplacement
                        {
                            mesh = assetBundle.LoadAsset<Mesh>(@"Assets\SkinMods\SurvivorsReturn\Meshes\GunMeshReturns.mesh"),
                            renderer = renderers[0]
                        },
                        new SkinDef.MeshReplacement
                        {
                            mesh = assetBundle.LoadAsset<Mesh>(@"Assets\SkinMods\SurvivorsReturn\Meshes\GunMeshReturns.mesh"),
                            renderer = renderers[3]
                        },
                        new SkinDef.MeshReplacement
                        {
                            mesh = assetBundle.LoadAsset<Mesh>(@"Assets\SkinMods\SurvivorsReturn\Meshes\Returns.mesh"),
                            renderer = renderers[6]
                        },
                    };
                });
                TryCatchThrow("Minion Skin Replacements", () =>
                {
                    skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
                });
                TryCatchThrow("Projectile Ghost Replacements", () =>
                {
                    skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
                });

                Array.Resize(ref skinController.skins, skinController.skins.Length + 1);
                skinController.skins[skinController.skins.Length - 1] = skin;

                BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(bodyPrefab)] = skinController.skins;
                CommandoBodyCommandoSkinAdded(skin, bodyPrefab);
            }
            catch (FieldException e)
            {
                InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\"");
                InstanceLogger.LogWarning($"Field causing issue: {e.Message}");
                InstanceLogger.LogError(e.InnerException);
            }
            catch (Exception e)
            {
                InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\"");
                InstanceLogger.LogError(e);
            }
        }

        static partial void HuntressBodyHuntressSkinAdded(SkinDef skinDef, GameObject bodyPrefab);

        private static void AddHuntressBodyHuntressSkin()
        {
            var bodyName = "HuntressBody";
            var skinName = "Huntress";
            try
            {
                var bodyPrefab = BodyCatalog.FindBodyPrefab(bodyName);
                if (!bodyPrefab)
                {
                    InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin because \"{bodyName}\" doesn't exist");
                    return;
                }

                var modelLocator = bodyPrefab.GetComponent<ModelLocator>();
                if (!modelLocator)
                {
                    InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\" because it doesn't have \"ModelLocator\" component");
                    return;
                }

                var mdl = modelLocator.modelTransform.gameObject;
                var skinController = mdl ? mdl.GetComponent<ModelSkinController>() : null;
                if (!skinController)
                {
                    InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\" because it doesn't have \"ModelSkinController\" component");
                    return;
                }

                var renderers = mdl.GetComponentsInChildren<Renderer>(true);

                var skin = ScriptableObject.CreateInstance<SkinDef>();
                TryCatchThrow("Icon", () =>
                {
                    skin.icon = assetBundle.LoadAsset<Sprite>(@"Assets\SkinMods\SurvivorsReturn\Icons\HuntressIcon.png");
                });
                skin.name = skinName;
                skin.nameToken = "ORBITAL_SKIN_HUNTRESS_NAME";
                skin.rootObject = mdl;
                TryCatchThrow("Base Skins", () =>
                {
                    skin.baseSkins = new SkinDef[] 
                    { 
                        skinController.skins[0],
                    };
                });
                TryCatchThrow("Unlockable Name", () =>
                {
                    skin.unlockableDef = ContentManager.unlockableDefs.FirstOrDefault(def => def.cachedName == "Huntress_Returns");
                });
                TryCatchThrow("Game Object Activations", () =>
                {
                    skin.gameObjectActivations = new SkinDef.GameObjectActivation[]
                    {
                        new SkinDef.GameObjectActivation
                        {
                            gameObject = renderers[11].gameObject,
                            shouldActivate = false
                        },
                    };
                });
                TryCatchThrow("Renderer Infos", () =>
                {
                    skin.rendererInfos = new CharacterModel.RendererInfo[]
                    {
                        new CharacterModel.RendererInfo
                        {
                            defaultMaterial = assetBundle.LoadAsset<Material>(@"Assets/Resources/HNTRS/borb.mat"),
                            defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                            ignoreOverlays = false,
                            renderer = renderers[1]
                        },
                        new CharacterModel.RendererInfo
                        {
                            defaultMaterial = assetBundle.LoadAsset<Material>(@"Assets/Resources/HNTRS/huntgr.mat"),
                            defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                            ignoreOverlays = false,
                            renderer = renderers[10]
                        },
                    };
                });
                TryCatchThrow("Mesh Replacements", () =>
                {
                    skin.meshReplacements = new SkinDef.MeshReplacement[]
                    {
                        new SkinDef.MeshReplacement
                        {
                            mesh = assetBundle.LoadAsset<Mesh>(@"Assets\SkinMods\SurvivorsReturn\Meshes\HuntressMesh.002.mesh"),
                            renderer = renderers[10]
                        },
                        new SkinDef.MeshReplacement
                        {
                            mesh = assetBundle.LoadAsset<Mesh>(@"Assets\SkinMods\SurvivorsReturn\Meshes\BowMesh.002.mesh"),
                            renderer = renderers[1]
                        },
                    };
                });
                TryCatchThrow("Minion Skin Replacements", () =>
                {
                    skin.minionSkinReplacements = Array.Empty<SkinDef.MinionSkinReplacement>();
                });
                TryCatchThrow("Projectile Ghost Replacements", () =>
                {
                    skin.projectileGhostReplacements = Array.Empty<SkinDef.ProjectileGhostReplacement>();
                });

                Array.Resize(ref skinController.skins, skinController.skins.Length + 1);
                skinController.skins[skinController.skins.Length - 1] = skin;

                BodyCatalog.skins[(int)BodyCatalog.FindBodyIndex(bodyPrefab)] = skinController.skins;
                HuntressBodyHuntressSkinAdded(skin, bodyPrefab);
            }
            catch (FieldException e)
            {
                InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\"");
                InstanceLogger.LogWarning($"Field causing issue: {e.Message}");
                InstanceLogger.LogError(e.InnerException);
            }
            catch (Exception e)
            {
                InstanceLogger.LogWarning($"Failed to add \"{skinName}\" skin to \"{bodyName}\"");
                InstanceLogger.LogError(e);
            }
        }

        private static void TryCatchThrow(string message, Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                throw new FieldException(message, e);
            }
        }

        private class FieldException : Exception
        {
            public FieldException(string message, Exception innerException) : base(message, innerException) { }
        }
    }
}