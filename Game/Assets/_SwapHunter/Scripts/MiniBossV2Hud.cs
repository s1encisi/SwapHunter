using UnityEngine;namespace SwapHunter{public sealed partial class DemoGame{
 void DrawMiniBossV2Hud(){
 if(!IsPlaying||!player)return;
 foreach(var enemy in enemies){if(!enemy||!enemy.Alive||!enemy.miniBoss||Vector3.Distance(player.transform.position,enemy.transform.position)>85)continue;
 var mini=enemy.miniBoss;float x=viewWidth-565;Fill(new Rect(x,135,533,108),new Color(.08f,.025f,.035f,.94f));
 Text(mini.DisplayName+" · "+Mathf.CeilToInt(enemy.health)+" / "+Mathf.CeilToInt(enemy.maximumHealth),new Rect(x+15,145,503,28),22,new Color(1,.7f,.4f));
 Fill(new Rect(x+15,178,503,5),new Color(.3f,.1f,.1f));Fill(new Rect(x+15,178,503*enemy.health/enemy.maximumHealth,5),new Color(1,.45f,.22f));
 Text(mini.Warning+(mini.SilenceResistanceLeft>0?" · 抗沉默 "+mini.SilenceResistanceLeft.ToString("0.0")+"s":""),new Rect(x+15,194,503,42),17,Paper);break;
 }
 }
}}

