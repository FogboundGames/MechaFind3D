using UnityEngine;
using UnityEngine.UI;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Shows/hides the Win and Lose popup panels built under UI_Canvas, and wires their
    /// buttons to the real game flow (LevelManager). Attached to UI_Canvas.
    /// </summary>
    public class WinLosePanelController : MonoBehaviour
    {
        public static WinLosePanelController Instance { get; private set; }

        private GameObject winPanel;
        private GameObject losePanel;

        private void Awake()
        {
            Instance = this;

            Transform winT = transform.Find("WinPanel");
            Transform loseT = transform.Find("LosePanel");
            winPanel = winT != null ? winT.gameObject : null;
            losePanel = loseT != null ? loseT.gameObject : null;

            WireButton(winT, "PopupPanel/ActionButton", () =>
            {
                HideAll();
                if (LevelManager.Instance != null) LevelManager.Instance.LoadNextLevel();
            });
            WireButton(winT, "PopupPanel/HomeText", HideAll);

            WireButton(loseT, "PopupPanel/ActionButton", () =>
            {
                HideAll();
                if (LevelManager.Instance != null) LevelManager.Instance.RestartCurrentLevel();
            });
            WireButton(loseT, "PopupPanel/HomeText", HideAll);

            HideAll();
        }

        private static void WireButton(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            if (root == null) return;
            Transform t = root.Find(path);
            if (t == null) return;

            Button btn = t.GetComponent<Button>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(action);
        }

        public void ShowWin()
        {
            if (losePanel != null) losePanel.SetActive(false);
            if (winPanel != null) winPanel.SetActive(true);
        }

        public void ShowLose()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(true);
        }

        public void HideAll()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
        }
    }
}
