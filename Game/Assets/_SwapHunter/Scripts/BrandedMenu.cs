using UnityEngine;
namespace SwapHunter
{
    public sealed partial class DemoGame
    {
        Texture2D menuKeyArt;
        void BrandedMenu()
        {
            if(!menuKeyArt)menuKeyArt=Resources.Load<Texture2D>("UI/Homecoming/menu-harbor-v2");
            Fill(FullScreenGuiRect,Ink);
            if(menuKeyArt)GUI.DrawTexture(FullScreenGuiRect,menuKeyArt,ScaleMode.ScaleAndCrop);
            float left=Mathf.Max(64,viewWidth*.065f),width=Mathf.Min(440,viewWidth*.34f);
            Fill(new Rect(0,FullScreenGuiRect.yMin,left+width+65,FullScreenGuiRect.height),new Color(.025f,.048f,.059f,.70f));
            Text("SWAPHUNTER",new Rect(left,104,width,30),18,Amber);
            Text("换位猎手",new Rect(left-4,167,width+45,100),72,Paper);
            Text("归航行动",new Rect(left,286,width,48),31,Paper);
            Text("把回来的路，留给每个人。",new Rect(left,352,width+20,40),19,new Color(.72f,.80f,.79f));
            Fill(new Rect(left,414,width,1),new Color(.45f,.53f,.51f,.5f));
            if(Button("开始行动    →",new Rect(left,464,width,64),true))OpenHomecoming();
            if(Button("设置",new Rect(left,548,width,52)))OpenSettings();
            if(Button("退出游戏",new Rect(left,620,width,52)))Application.Quit();
            Text("归航行动 / 12 章完整旅程",new Rect(left,788,width+60,28),15,Muted);
            Text("v"+Application.version,new Rect(viewWidth-220,840,160,24),13,new Color(.73f,.80f,.79f),TextAnchor.MiddleRight);
        }
    }
}
