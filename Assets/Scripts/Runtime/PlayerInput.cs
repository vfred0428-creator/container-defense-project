using UnityEngine;

namespace ContainerDefense
{
    public interface IPlayerInput
    {
        Vector2 Movement { get; }
        bool InteractPressed { get; }
        bool PausePressed { get; }
        bool SpectatePressed { get; }
        int UpgradePressed { get; }
    }

    // Replace this adapter with touch controls later; match rules never read device input.
    public sealed class DesktopPlayerInput : IPlayerInput
    {
        public Vector2 Movement
        {
            get
            {
                float x = (Key(KeyCode.D, KeyCode.RightArrow) ? 1 : 0) - (Key(KeyCode.A, KeyCode.LeftArrow) ? 1 : 0);
                float y = (Key(KeyCode.W, KeyCode.UpArrow) ? 1 : 0) - (Key(KeyCode.S, KeyCode.DownArrow) ? 1 : 0);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1);
            }
        }
        public bool InteractPressed { get { return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space); } }
        public bool PausePressed { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public bool SpectatePressed { get { return Input.GetKeyDown(KeyCode.Tab); } }
        public int UpgradePressed
        {
            get
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
                return -1;
            }
        }
        private static bool Key(KeyCode a, KeyCode b) { return Input.GetKey(a) || Input.GetKey(b); }
    }
}
