using System;
using UnityEngine;

namespace SwapHunter
{
    // Created explicitly through MissionCatalogEditor.PrepareDefaultConfiguration.
    // Runtime never creates, repairs or substitutes this asset.
    public sealed class MissionConfiguration : ScriptableObject
    {
        [Tooltip("固定保留 12 条，level 与数组位置 0..11 一致。目标和遭遇 id 是地图绑定键，不作为显示名称修改。")]
        public MissionDefinition[] missions;

        public void Validate() { MissionCatalog.Validate(missions); }
    }
}
