using UnityEngine;
namespace SwapHunter
{
    // Four new CC0 bodies; the adapter applies a display-only holding pose without combat components.
    public static class ClassVisualFactory
    {
        public static GameObject CreateClassModel(int classId,Transform parent)
            =>ActorVisualRig.CreateClassDisplay(classId,parent);
        public static GameObject CreatePlayerShadowBody(int classId,Transform parent)
            =>ActorVisualRig.CreatePlayerShadowBody(classId,parent);
    }
}