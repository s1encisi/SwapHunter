using UnityEngine;
namespace SwapHunter
{
 public sealed partial class DemoGame
 {
  Texture2D menuKeyArt;
  void BrandedMenu()
  {
   if(!menuKeyArt)menuKeyArt=Resources.Load<Texture2D>("Brand/swaphunter-key-art-v05");
   if(menuKeyArt)GUI.DrawTexture(FullScreenGuiRect,menuKeyArt,ScaleMode.ScaleAndCrop);
   float left=62,width=Mathf.Min(510,viewWidth*.35f-55);
   // Actual controls sit on the deliberately quiet side of the full illustrated composition.
   Fill(new Rect(0,0,width+left+46,900),new Color(.009f,.023f,.044f,.27f));
   Fill(new Rect(left,91,44,4),Cyan);
   Text("SWAPHUNTER",new Rect(left,113,width,32),19,new Color(.64f,.87f,.89f));
   Text("换位猎手",new Rect(left-5,165,width+35,105),74,Color.white);
   Text("夺取位置。决定战局。",new Rect(left,290,width+20,42),26,new Color(.76f,.95f,.95f));
   Text("观察空间，改变交火的两端。",new Rect(left,345,width+15,34),18,new Color(.68f,.77f,.81f));
   if(Button("开始相位行动     →",new Rect(left,422,width,61),true)){OpenExpeditionBoard();expeditionTab=0;}
   if(Button("逐步实战教学",new Rect(left,497,width,52)))StartTutorial();
   if(Button("经典战役",new Rect(left,563,(width-12)/2,46)))StartRun();
   if(Button("经典合约",new Rect(left+(width+12)/2,563,(width-12)/2,46)))OpenCampaignBoard();
   bool checkpoint=RunStorage.Checkpoint>0;float y=623;
   if(checkpoint){if(Button("继续战役检查点",new Rect(left,y,width,42)))StartRun(false,true);y+=54;}
   if(Button("自由试招",new Rect(left,y,(width-12)/2,44)))StartRun(true);
   if(Button("设置",new Rect(left+(width+12)/2,y,(width-12)/2,44)))OpenSettings();
   if(Button("退出游戏",new Rect(left,y+57,width,42)))Application.Quit();
   Text("Tab / 方向键选择 · Enter 确认",new Rect(left,837,width+30,24),14,new Color(.58f,.69f,.74f));
   Text("FPS / 空间换位 / 高机动战斗",new Rect(viewWidth-475,790,415,30),18,new Color(.84f,.96f,.97f),TextAnchor.MiddleRight);
   Text("v"+Application.version,new Rect(viewWidth-250,837,190,24),14,new Color(.63f,.74f,.78f),TextAnchor.MiddleRight);
  }
 }
}
