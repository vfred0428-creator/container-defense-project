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
            set = sprites; visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = set.Body ?? set.Frame(0);
        }
        public void Present(Vector3 position,CharacterPose pose,float time,bool right,int order)
        {
            Pose = pose;
            if (set.Body != null) { PresentBody(position,pose,time,right,order); return; }
            int frame = animator.Frame(pose,time);
            transform.position = position; transform.localScale = animator.Scale(pose,time) * 2.55f; transform.rotation = Quaternion.identity;
            visual.sprite = set.Frame(frame); visual.sortingOrder = order;
            // Milo's generated run cycle has opposing view directions; normalize at presentation.
            visual.flipX = pose == CharacterPose.Run && (right ^ (set.SkinId == "milo_default" && frame == 2));
            visual.color = pose == CharacterPose.Eliminated ? new Color(.65f,.65f,.75f,.75f) : Color.white;
        }
        // Single-pose vinyl figure: motion comes from hop, bob, tilt and squash rather than frames.
        private void PresentBody(Vector3 position,CharacterPose pose,float time,bool right,int order)
        {
            visual.sprite = set.Body; visual.sortingOrder = order; visual.flipX = !right;
            float lift = 0, tilt = 0; Vector3 scale = animator.Scale(pose,time);
            switch (pose) {
                case CharacterPose.Run: lift = Mathf.Abs(Mathf.Sin(time * 11)) * .22f; tilt = Mathf.Sin(time * 11) * 6; break;
                case CharacterPose.Victory: case CharacterPose.Upgrade: lift = Mathf.Abs(Mathf.Sin(time * 7)) * .35f; break;
                case CharacterPose.Hurt: position.x += Mathf.Sin(time * 60) * .06f; tilt = -8; break;
                case CharacterPose.Eliminated: tilt = right ? -80 : 80; break;
                default: lift = (Mathf.Sin(time * 2.4f) + 1) * .025f; break;
            }
            transform.position = position + new Vector3(0,lift,0); transform.rotation = Quaternion.Euler(0,0,tilt);
            transform.localScale = scale * 2.55f;
            visual.color = pose == CharacterPose.Eliminated ? new Color(.6f,.6f,.7f,.7f) : pose == CharacterPose.Hurt ? new Color(1,.7f,.7f) : Color.white;
        }
    }
}
