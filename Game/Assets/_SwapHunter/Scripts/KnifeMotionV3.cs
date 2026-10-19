using UnityEngine;

namespace SwapHunter
{
    public readonly struct KnifeMotionV3Pose
    {
        public readonly Vector3 handLocal;
        public readonly Quaternion gripRotation;
        public readonly float leftSupport, fingerClosure;

        public KnifeMotionV3Pose(Vector3 hand, Quaternion rotation, float support, float closure)
        { handLocal = hand; gripRotation = rotation; leftSupport = support; fingerClosure = closure; }
    }

    // Camera-local wrist/grip targets for the existing arm IK. This sampler owns
    // no weapon instance, animation clock, hinge, damage event or inventory state.
    public static class KnifeMotionV3
    {
        public const float EquipReadySeconds = .30f, EquipEndSeconds = .40f;
        public const float InspectEndSeconds = 1.20f, LightEndSeconds = .50f, HeavyEndSeconds = .90f;
        static readonly Vector3 RestHand = new Vector3(.20f, -.22f, .46f);

        public static KnifeMotionV3Pose Sample(int knifeId, float equipSeconds, float inspectSeconds, float attackSeconds, bool heavy)
        {
            int id = Mathf.Clamp(knifeId, 0, 5);
            Quaternion rest = RestRotation(id);
            if (attackSeconds >= 0 && attackSeconds <= (heavy ? HeavyEndSeconds : LightEndSeconds))
                return heavy ? Heavy(id, attackSeconds, rest) : Light(id, attackSeconds, rest);
            if (equipSeconds >= 0 && equipSeconds <= EquipEndSeconds) return Equip(id, equipSeconds, rest);
            if (inspectSeconds >= 0 && inspectSeconds <= InspectEndSeconds) return Inspect(id, inspectSeconds, rest);
            return Pose(RestHand, rest, .08f, .92f);
        }

        static Quaternion RestRotation(int id)
        {
            // Authored blade/handle axis is +Y. The karambit stays in reverse
            // grip instead of inheriting the straight knives' upright hold.
            if (id == 2) return Quaternion.Euler(168, -12, 14);
            return Quaternion.Euler(-12, -12, id == 1 ? -5 : id == 5 ? 4 : 0);
        }

        static KnifeMotionV3Pose Equip(int id, float seconds, Quaternion rest)
        {
            Vector3 low = new Vector3(.27f, -.48f, .24f), lifted = new Vector3(.23f, -.30f, .39f);
            Quaternion lowRotation = rest * Quaternion.Euler(48, 18, -12);
            Quaternion liftedRotation = rest * Quaternion.Euler(12, 5, -4);
            if (seconds <= .10f)
                return Segment(seconds / .10f, low, low + new Vector3(-.01f, .04f, .01f), lifted + new Vector3(.015f, -.06f, -.04f), lifted,
                    lowRotation, liftedRotation, .02f, .04f, .60f, .90f);
            if (seconds <= EquipReadySeconds)
                return Segment((seconds - .10f) / .20f, lifted, new Vector3(.23f, -.25f, .44f), new Vector3(.205f, -.215f, .465f), RestHand,
                    liftedRotation, rest, .04f, .08f, .90f, .96f);
            // The ready pose is reached at .30. Only the grip pressure settles
            // until .40, so attacking at the ready point does not pop the hand.
            return Pose(RestHand, rest, .08f, Mathf.Lerp(.96f, .92f, Ease5((seconds - .30f) / .10f)));
        }

        static KnifeMotionV3Pose Inspect(int id, float seconds, Quaternion rest)
        {
            float support = id == 1 || id == 5 ? .50f : id == 2 ? .12f : .28f;
            float roll = id == 3 ? .65f : 1;
            Vector3 raised = new Vector3(.08f, -.09f, .48f), first = new Vector3(.03f, -.095f, .51f), second = new Vector3(.13f, -.105f, .50f);
            Quaternion up = rest * Quaternion.Euler(-6, -12, -12 * roll);
            Quaternion firstFace = rest * Quaternion.Euler(-8, -35, -32 * roll);
            Quaternion secondFace = rest * Quaternion.Euler(7, 45, 25 * roll);
            if (seconds <= .22f)
                return Segment(seconds / .22f, RestHand, new Vector3(.18f, -.16f, .44f), new Vector3(.10f, -.10f, .46f), raised,
                    rest, up, .08f, support, .92f, .86f);
            if (seconds <= .55f)
                return Segment((seconds - .22f) / .33f, raised, new Vector3(.06f, -.06f, .49f), new Vector3(.02f, -.08f, .51f), first,
                    up, firstFace, support, support, .86f, .80f);
            if (seconds <= .85f)
                return Segment((seconds - .55f) / .30f, first, new Vector3(.055f, -.08f, .52f), new Vector3(.11f, -.09f, .515f), second,
                    firstFace, secondFace, support, support * .7f, .80f, .88f);
            return Segment((seconds - .85f) / .35f, second, new Vector3(.16f, -.13f, .49f), new Vector3(.21f, -.19f, .465f), RestHand,
                secondFace, rest, support * .7f, .08f, .88f, .92f);
        }

        static KnifeMotionV3Pose Light(int id, float seconds, Quaternion rest)
        {
            // Preparation coils to the right; the blade crosses the centre in
            // an arc, then the elbow folds down before the wrist returns ready.
            float span = id == 2 ? .82f : id == 1 ? 1.04f : 1;
            Vector3 prepared = new Vector3(.30f, -.16f, .36f);
            Vector3 crossed = new Vector3(.20f - .27f * span, -.24f, .53f);
            Vector3 recovered = new Vector3(.15f, -.28f, .43f);
            Quaternion coil = rest * Quaternion.Euler(18, 28, -52 * span);
            Quaternion slash = rest * Quaternion.Euler(24, -34, 62 * span);
            Quaternion folded = rest * Quaternion.Euler(10, -10, 12);
            if (seconds <= .10f)
                return Segment(seconds / .10f, RestHand, new Vector3(.235f, -.21f, .42f), new Vector3(.29f, -.18f, .37f), prepared,
                    rest, coil, .08f, .03f, .92f, 1);
            if (seconds <= .18f)
                return Segment((seconds - .10f) / .08f, prepared, new Vector3(.26f, -.08f, .58f), new Vector3(.03f, -.14f, .63f), crossed,
                    coil, slash, .03f, 0, 1, 1);
            if (seconds <= .34f)
                return Segment((seconds - .18f) / .16f, crossed, new Vector3(.02f, -.36f, .48f), new Vector3(.12f, -.32f, .36f), recovered,
                    slash, folded, 0, .04f, 1, .96f);
            return Segment((seconds - .34f) / .16f, recovered, new Vector3(.17f, -.265f, .44f), new Vector3(.20f, -.23f, .46f), RestHand,
                folded, rest, .04f, .08f, .96f, .92f);
        }

        static KnifeMotionV3Pose Heavy(int id, float seconds, Quaternion rest)
        {
            // A separate chamber/thrust/withdrawal path, not the light slash
            // stretched in time. At full extension, authored blade +Y is +Z.
            Vector3 chamber = new Vector3(.25f, id == 5 ? -.30f : -.28f, .27f);
            Vector3 extension = new Vector3(.16f, -.19f, .64f), withdrawal = new Vector3(.23f, -.30f, .37f);
            Quaternion pointForward = Quaternion.AngleAxis(id == 2 ? 180 : id == 1 ? -7 : id == 5 ? 6 : 0, Vector3.forward)
                * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
            Quaternion aimed = Quaternion.Slerp(rest, pointForward, .86f);
            Quaternion retracted = rest * Quaternion.Euler(38, -8, 5);
            if (seconds <= .26f)
                return Segment(seconds / .26f, RestHand, new Vector3(.22f, -.24f, .40f), chamber + new Vector3(-.01f, .015f, .015f), chamber,
                    rest, aimed, .08f, .22f, .92f, 1);
            if (seconds <= .40f)
                return Segment((seconds - .26f) / .14f, chamber, new Vector3(.23f, -.26f, .42f), new Vector3(.18f, -.20f, .59f), extension,
                    aimed, pointForward, .22f, .03f, 1, 1);
            if (seconds <= .70f)
                return Segment((seconds - .40f) / .30f, extension, new Vector3(.15f, -.22f, .57f), new Vector3(.25f, -.31f, .42f), withdrawal,
                    pointForward, retracted, .03f, .15f, 1, .97f);
            return Segment((seconds - .70f) / .20f, withdrawal, new Vector3(.23f, -.27f, .39f), new Vector3(.20f, -.235f, .45f), RestHand,
                retracted, rest, .15f, .08f, .97f, .92f);
        }

        static KnifeMotionV3Pose Segment(float fraction, Vector3 a, Vector3 controlA, Vector3 controlB, Vector3 b,
            Quaternion rotationA, Quaternion rotationB, float supportA, float supportB, float closureA, float closureB)
        {
            // Quintic timing has zero velocity and acceleration at both ends;
            // shared endpoints make the cubic hand paths C2 across phases.
            float t = Ease5(fraction), u = 1 - t;
            Vector3 hand = u * u * u * a + 3 * u * u * t * controlA + 3 * u * t * t * controlB + t * t * t * b;
            return Pose(hand, Quaternion.Slerp(rotationA, rotationB, t), Mathf.Lerp(supportA, supportB, t), Mathf.Lerp(closureA, closureB, t));
        }
        static float Ease5(float value)
        { float t = Mathf.Clamp01(value); return t * t * t * (t * (6 * t - 15) + 10); }
        static KnifeMotionV3Pose Pose(Vector3 hand, Quaternion rotation, float support, float closure)
        { return new KnifeMotionV3Pose(hand, rotation, Mathf.Clamp01(support), Mathf.Clamp01(closure)); }
    }
}
