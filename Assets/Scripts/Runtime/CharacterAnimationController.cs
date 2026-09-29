using UnityEngine;

namespace ContainerDefense
{
    public enum CharacterPose { Idle, Run, Sleep, Attack, Hurt, Eliminated, Victory, Select, Upgrade }
    public sealed class CharacterAnimationController
    {
        public int Frame(CharacterPose pose,float time)
        {
            switch (pose)
            {
                case CharacterPose.Run: return 1 + (Mathf.FloorToInt(time * 9) & 1);
                case CharacterPose.Sleep: return 3;
                case CharacterPose.Attack: return 4;
                case CharacterPose.Hurt: return 5;
                case CharacterPose.Eliminated: return 6;
                case CharacterPose.Victory: case CharacterPose.Upgrade: return 7;
                case CharacterPose.Select: return 8;
                default: return 0;
            }
        }
        public Vector3 Scale(CharacterPose pose,float time)
        {
            float bounce = Mathf.Sin(time * (pose == CharacterPose.Run ? 18 : pose == CharacterPose.Sleep ? 2 : 3));
            float amount = pose == CharacterPose.Eliminated ? 0 : pose == CharacterPose.Run ? .035f : .012f;
            return new Vector3(1 - bounce * amount * .5f,1 + bounce * amount,1);
        }
    }
}
