using UnityEngine;
namespace SwapHunter
{
    public static class PrecisionImpact
    {
        public static void Spawn(Vector3 point, bool head)
        {
            if(head)Shapes.Burst(point,new Color(1f,.56f,.25f),.27f);
            else Shapes.Trace(point-Vector3.up*.035f,point+Vector3.up*.035f,new Color(.78f,.90f,.92f),.09f,.022f);
        }
    }
    public sealed partial class DemoGame
    {
        void DrawPrecisionHitFeedback(float x,float y)
        {
            if(!options.hitMarkers||!player||player.hitFlash<=0)return;
            bool head=player.HitRegionLabel=="头部", kill=player.killFlash>0;
            float life=head?.32f:kill?.25f:.14f;
            float alpha=Mathf.Clamp01(player.hitFlash/life);
            Color color=head?new Color(1,.64f,.28f,alpha):kill?new Color(.85f,.94f,.88f,alpha):new Color(.88f,.94f,.95f,alpha);
            float gap=head?12:9,length=head?10:6;
            for(int i=0;i<4;i++)
            {
                float a=45+i*90;Vector2 direction=new Vector2(Mathf.Cos(a*Mathf.Deg2Rad),Mathf.Sin(a*Mathf.Deg2Rad));
                Matrix4x4 old=GUI.matrix;Vector2 pivot=new Vector2(x,y)+direction*gap;
                GUIUtility.RotateAroundPivot(a,pivot);Fill(new Rect(pivot.x,pivot.y-1,length,head?3:2),color);GUI.matrix=old;
            }
            if(head)
            {
                Text(kill?"爆头击破":"爆头命中",new Rect(x-90,y+35,180,28),kill?19:17,color,TextAnchor.MiddleCenter);
                if(kill)Fill(new Rect(x-18,y+68,36,2),color);
            }
            else if(kill)Text("击破",new Rect(x-65,y+32,130,24),16,color,TextAnchor.MiddleCenter);
        }
    }
}
