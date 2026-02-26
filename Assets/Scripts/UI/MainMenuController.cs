using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonGame.UI
{
    /// <summary>
    /// Put this on a GameObject (e.g. Canvas or empty "MenuController"). Wire your menu buttons to these methods in the Button's On Click list.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "Town";

        /// <summary>
        /// Loads the first game scene (e.g. Town). Call from Play button's On Click.
        /// </summary>
        public void LoadFirstScene()
        {
            SceneManager.LoadScene(firstSceneName);
        }

        /// <summary>
        /// Loads a scene by name. Call from a button's On Click and set the string in the Inspector to the scene name.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Quits the application. Call from Quit button's On Click. Only works in a build, not in the Editor.
        /// </summary>
        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
