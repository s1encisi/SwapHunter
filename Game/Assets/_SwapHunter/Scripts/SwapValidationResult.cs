using UnityEngine;

namespace SwapHunter
{
    // Stable protocol values for gameplay, telemetry and QA. Display text is not a rule.
    public enum SwapFailureReason
    {
        None = 0, NotPlaying = 1, ActionInProgress = 2, Cooldown = 3,
        Preparing = 4, InvalidTarget = 5, OutOfRange = 6,
        LineOfSightBlocked = 7, DestinationBlocked = 8, Interference = 9,
        RelocationFailed = 10, Canceled = 11
    }

    public readonly struct SwapValidationResult
    {
        public SwapFailureReason Code { get; }
        public string FieldId { get; }
        public bool IsValid => Code == SwapFailureReason.None;
        public SwapValidationResult(SwapFailureReason code, string fieldId = null)
        { Code = code; FieldId = fieldId; }

        public string ToDisplayText(float range)
        {
            switch (Code)
            {
                case SwapFailureReason.None: return "可换位";
                case SwapFailureReason.NotPlaying: return "当前不能换位";
                case SwapFailureReason.ActionInProgress: return "动作进行中";
                case SwapFailureReason.Cooldown: return "冷却中";
                case SwapFailureReason.Preparing: return "相位准备中";
                case SwapFailureReason.InvalidTarget: return "寻找目标";
                case SwapFailureReason.OutOfRange: return "超出 " + range.ToString("0.#") + " 米";
                case SwapFailureReason.LineOfSightBlocked: return "视线被挡";
                case SwapFailureReason.DestinationBlocked: return "落点受阻";
                case SwapFailureReason.Interference: return "相位干扰 · 离开场区或关闭发射器";
                case SwapFailureReason.RelocationFailed: return "换位取消：导航恢复失败";
                case SwapFailureReason.Canceled: return "换位已取消";
                default: return "换位不可用";
            }
        }

        public override string ToString() => FieldId == null ? Code.ToString() : Code + ":" + FieldId;
    }
}
