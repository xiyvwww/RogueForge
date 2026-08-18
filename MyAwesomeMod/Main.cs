using BepInEx;
using BepInEx.Logging;
using RogueLibsCore;

namespace MyAwesomeMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(RogueLibs.GUID, RogueLibs.CompiledVersion)]
    public class MyAwesomePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "myawesomeusername.streetsofrogue.myawesomemod";
        public const string PluginName = "My Awesome Mod";
        public const string PluginVersion = "0.1.0";

        public new static ManualLogSource Logger = null!;

        public void Awake()
        {
            Logger = base.Logger;
            RogueLibs.LoadFromAssembly();
            // 自定义建筑库：加载本程序集中的 [RLSetup] 注册（创建自定义建筑元数据）
            RogueForge.CustomObjects.LoadFromAssembly();
            // 初始化库：注册全部 patch（prefab 注册/编辑器注入/网格重画/生成重建）
            RogueForge.CustomBuildingsPlugin.Initialize(this);

            /* My Awesome Code */

        }
    }
}
