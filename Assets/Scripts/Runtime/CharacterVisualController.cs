using UnityEngine;

namespace ContainerDefense
{
    public sealed class CharacterVisualController : MonoBehaviour
    {
        public CharacterPose Pose { get; private set; }
        public string SkinId { get { return set.SkinId; } }
        private CharacterSpriteSet set;
        private SpriteRenderer visual;
        private readonly CharacterAnimationController animator = new CharacterAnimationController();
        public void Initialize(CharacterSpriteSet sprites)
        {
            set = sprites; visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = set.Frame(0);
        }
        public void Present(Vector3 position,CharacterPose pose,float time,bool right,int order)
        {
            Pose = pose; int frame = animator.Frame(pose,time);
            transform.position = position; transform.localScale = animator.Scale(pose,time) * 2.55f;
            visual.sprite = set.Frame(frame); visual.sortingOrder = order;
            // Milo's generated run cycle has opposing view directions; normalize at presentation.
            visual.flipX = pose == CharacterPose.Run && (right ^ (set.SkinId == "milo_default" && frame == 2));
            visual.color = pose == CharacterPose.Eliminated ? new Color(.65f,.65f,.75f,.75f) : Color.white;
        }
    }
}

