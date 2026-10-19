namespace SwapHunter{public sealed partial class PlayerMotor{
 public void UnequipV2Handheld(){if(meleeEquipment)meleeEquipment.Unequip();}
}}


namespace SwapHunter {public sealed partial class DemoGame{
 public bool BeginV2InventoryUse(int type){if(!player)return false;expeditionInventory=false;SetState(RunState.Playing);return player.GetComponent<ConsumableUseV2>().Begin(type);}
}}
