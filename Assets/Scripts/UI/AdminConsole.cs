using System;
using System.Collections.Generic;
using System.Linq;
using DungeonGame.Combat;
using DungeonGame.Core;
using DungeonGame.Items;
using DungeonGame.Player;
using DungeonGame.Weapons;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonGame.UI
{
    /// <summary>
    /// In-game admin/developer console. Toggle with ` (backquote / tilde).
    /// Supports commands: god, fly, noclip, respawn, give, kill, heal, tp, spawn, help.
    /// Server-authoritative: commands that change game state go through RPCs.
    /// Auto-bootstraps itself on scene load.
    /// </summary>
    public class AdminConsole : MonoBehaviour
    {
        public static AdminConsole Instance { get; private set; }

        private bool _isOpen;
        private string _inputText = "";
        private readonly List<string> _log = new();
        private const int MaxLogLines = 200;
        private Vector2 _scrollPos;

        // Cheat states (per local player)
        private bool _godMode;
        private bool _flyMode;
        private float _flySpeed = 15f;
        private float _savedMoveSpeed;
        private float _savedGravity;

        // UI
        private Canvas _canvas;
        private GameObject _panel;
        private InputField _inputField;
        private Text _logText;
        private ScrollRect _scrollRect;

        // History
        private readonly List<string> _history = new();
        private int _historyIndex = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[AdminConsole]");
            go.AddComponent<AdminConsole>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildUI();
            _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // Toggle console with backquote key (`)
            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                ToggleConsole();
                return;
            }

            if (!_isOpen) return;

            // Handle fly mode movement
            if (_flyMode)
                UpdateFlyMode();

            // Enter to submit
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                SubmitCommand();
            }

            // History navigation
            if (Keyboard.current.upArrowKey.wasPressedThisFrame && _history.Count > 0)
            {
                _historyIndex = Mathf.Max(0, _historyIndex - 1);
                _inputText = _history[_historyIndex];
                if (_inputField != null) _inputField.text = _inputText;
            }
            if (Keyboard.current.downArrowKey.wasPressedThisFrame && _history.Count > 0)
            {
                _historyIndex = Mathf.Min(_history.Count, _historyIndex + 1);
                _inputText = _historyIndex < _history.Count ? _history[_historyIndex] : "";
                if (_inputField != null) _inputField.text = _inputText;
            }
        }

        private void ToggleConsole()
        {
            _isOpen = !_isOpen;
            _panel.SetActive(_isOpen);

            if (_isOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (_inputField != null)
                {
                    _inputField.ActivateInputField();
                    _inputField.Select();
                }
            }
            else
            {
                // Only re-lock cursor if pause menu isn't open
                if (!PauseMenuController.IsPaused)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private void SubmitCommand()
        {
            if (string.IsNullOrWhiteSpace(_inputText)) return;

            string cmd = _inputText.Trim();
            Log($"> {cmd}");

            _history.Add(cmd);
            _historyIndex = _history.Count;

            ExecuteCommand(cmd);

            _inputText = "";
            if (_inputField != null)
            {
                _inputField.text = "";
                _inputField.ActivateInputField();
            }
        }

        // ─────────────────────── Command Execution ───────────────────────

        private void ExecuteCommand(string raw)
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string cmd = parts[0].ToLower();
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            switch (cmd)
            {
                case "help": CmdHelp(); break;
                case "god": CmdGodMode(); break;
                case "fly":
                case "noclip": CmdFlyMode(args); break;
                case "respawn": CmdRespawn(); break;
                case "give": CmdGiveItem(args); break;
                case "kill": CmdKill(); break;
                case "heal": CmdHeal(args); break;
                case "tp": CmdTeleport(args); break;
                case "speed": CmdSpeed(args); break;
                case "items": CmdListItems(); break;
                case "players": CmdListPlayers(); break;
                case "scene": CmdScene(); break;
                case "pos": CmdPos(); break;
                case "clear": _log.Clear(); RefreshLog(); break;
                default:
                    Log($"Unknown command: {cmd}. Type 'help' for a list.");
                    break;
            }
        }

        private void CmdHelp()
        {
            Log("=== Admin Console Commands ===");
            Log("  god          - Toggle god mode (invincible)");
            Log("  fly [speed]  - Toggle fly/noclip mode");
            Log("  noclip       - Same as fly");
            Log("  respawn      - Teleport to nearest spawn point");
            Log("  give <id>    - Give item by ID (use 'items' to list)");
            Log("  items        - List all available item IDs");
            Log("  kill         - Kill your player");
            Log("  heal [amt]   - Heal player (default: full)");
            Log("  tp <x> <y> <z> - Teleport to coordinates");
            Log("  speed <val>  - Set move speed");
            Log("  players      - List connected players");
            Log("  scene        - Show current scene name");
            Log("  pos          - Show your position");
            Log("  clear        - Clear console log");
        }

        // ─────────────── God Mode ───────────────

        private void CmdGodMode()
        {
            _godMode = !_godMode;
            Log($"God mode: {(_godMode ? "ON" : "OFF")}");

            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            // Server-side: set HP to max continuously
            var health = player.GetComponent<PlayerHealth>();
            if (health != null && _godMode)
            {
                health.HealRpc(health.MaxHp);
            }
        }

        // Apply god mode each frame (heal to full, prevent death)
        private void LateUpdate()
        {
            if (!_godMode) return;
            var player = GetLocalPlayer();
            if (player == null) return;

            var health = player.GetComponent<PlayerHealth>();
            if (health != null && health.Hp < health.MaxHp)
                health.HealRpc(health.MaxHp);
        }

        // ─────────────── Fly / Noclip Mode ───────────────

        private void CmdFlyMode(string[] args)
        {
            if (args.Length > 0 && float.TryParse(args[0], out float spd))
                _flySpeed = Mathf.Clamp(spd, 1f, 200f);

            _flyMode = !_flyMode;
            Log($"Fly mode: {(_flyMode ? "ON" : "OFF")} (speed: {_flySpeed})");

            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            var cc = player.GetComponent<CharacterController>();
            var motor = player.GetComponent<ThirdPersonMotor>();

            if (_flyMode)
            {
                // Disable CharacterController so we can move freely
                if (cc != null) cc.enabled = false;
                // Disable motor so it doesn't fight our movement
                if (motor != null) motor.enabled = false;
            }
            else
            {
                // Re-enable
                if (motor != null) motor.enabled = true;
                if (cc != null) cc.enabled = true;
            }
        }

        private void UpdateFlyMode()
        {
            var player = GetLocalPlayer();
            if (player == null) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Don't process movement if input field is focused
            if (_inputField != null && _inputField.isFocused) return;

            Vector3 move = Vector3.zero;
            Transform camT = Camera.main != null ? Camera.main.transform : player.transform;

            Vector3 forward = camT.forward;
            Vector3 right = camT.right;

            // WASD for horizontal, Space/Shift for vertical
            if (kb.wKey.isPressed) move += forward;
            if (kb.sKey.isPressed) move -= forward;
            if (kb.dKey.isPressed) move += right;
            if (kb.aKey.isPressed) move -= right;
            if (kb.spaceKey.isPressed) move += Vector3.up;
            if (kb.leftShiftKey.isPressed) move -= Vector3.up;

            float speedMul = kb.leftCtrlKey.isPressed ? 3f : 1f; // Ctrl for boost

            if (move.sqrMagnitude > 0.001f)
            {
                player.transform.position += move.normalized * _flySpeed * speedMul * Time.unscaledDeltaTime;
            }
        }

        // ─────────────── Respawn ───────────────

        private void CmdRespawn()
        {
            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            // Find nearest spawn point
            var spawns = UnityEngine.Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (spawns == null || spawns.Length == 0)
            {
                Log("No spawn points found in scene.");
                return;
            }

            var nearest = spawns.OrderBy(s => Vector3.Distance(s.transform.position, player.transform.position)).First();

            var motor = player.GetComponent<ThirdPersonMotor>();
            if (motor != null)
            {
                // Request server teleport
                motor.ServerTeleport(nearest.transform.position, nearest.transform.rotation);
                Log($"Respawned at spawn point ({nearest.transform.position})");
            }
            else
            {
                player.transform.position = nearest.transform.position;
                Log($"Respawned at {nearest.transform.position}");
            }

            // Also heal
            var health = player.GetComponent<PlayerHealth>();
            if (health != null) health.HealRpc(health.MaxHp);
        }

        // ─────────────── Give Item ───────────────

        private void CmdGiveItem(string[] args)
        {
            if (args.Length == 0)
            {
                Log("Usage: give <itemId>  — Use 'items' to list available IDs.");
                return;
            }

            string itemId = string.Join("_", args).ToLower();

            // Find from ItemRegistry
            var allItems = ItemRegistry.GetAll();
            if (allItems == null || allItems.Count == 0)
            {
                Log("ItemRegistry not available.");
                return;
            }

            // Fuzzy match: try exact, then contains
            WeaponConfig match = null;
            foreach (var item in allItems)
            {
                if (item == null) continue;
                if (item.weaponId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                {
                    match = item;
                    break;
                }
            }
            if (match == null)
            {
                foreach (var item in allItems)
                {
                    if (item == null) continue;
                    if (item.weaponId.IndexOf(itemId, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        match = item;
                        break;
                    }
                }
            }

            if (match == null)
            {
                Log($"Item '{itemId}' not found. Use 'items' to list available items.");
                return;
            }

            // Request server to spawn the item via HandSystem
            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            var handSystem = player.GetComponent<HandSystem>();
            if (handSystem != null)
            {
                // Use the existing RPC to spawn weapon
                GiveItemServerRpc(match.weaponId);
                Log($"Giving item: {match.weaponId} ({match.displayName})");
            }
            else
            {
                Log("No HandSystem on player.");
            }
        }

        /// <summary>
        /// Server RPC to give item. We find a static method or use reflection.
        /// Since AdminConsole is not a NetworkBehaviour, we call the HandSystem directly.
        /// </summary>
        private void GiveItemServerRpc(string weaponId)
        {
            // If we're the server/host, do it directly
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            var player = GetLocalPlayer();
            if (player == null) return;

            var handSystem = player.GetComponent<HandSystem>();
            if (handSystem == null) return;

            // Use the owner RPC approach — request spawn from the owning client
            // HandSystem already has RequestSpawnWeaponServerRpc which we can invoke via reflection
            // or we can use the public method if available.
            // Since the console owner IS the local player owner, we can call the RPC.
            var method = typeof(HandSystem).GetMethod("RequestSpawnWeaponServerRpc",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(handSystem, new object[] { weaponId });
            }
            else
            {
                Log("Could not invoke RequestSpawnWeaponServerRpc.");
            }
        }

        private void CmdListItems()
        {
            var allItems = ItemRegistry.GetAll();
            if (allItems == null || allItems.Count == 0)
            {
                Log("No items in registry (ItemRegistry not loaded?).");
                return;
            }

            Log($"=== Items ({allItems.Count}) ===");
            foreach (var item in allItems)
            {
                if (item == null) continue;
                string name = !string.IsNullOrEmpty(item.displayName) ? item.displayName : item.weaponId;
                Log($"  {item.weaponId} — {name}");
            }
        }

        // ─────────────── Kill ───────────────

        private void CmdKill()
        {
            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            var health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.DamageRpc(health.MaxHp * 999);
                Log("Killed player.");
            }
        }

        // ─────────────── Heal ───────────────

        private void CmdHeal(string[] args)
        {
            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            var health = player.GetComponent<PlayerHealth>();
            if (health == null) { Log("No PlayerHealth found."); return; }

            int amount = health.MaxHp;
            if (args.Length > 0 && int.TryParse(args[0], out int a))
                amount = a;

            health.HealRpc(amount);
            Log($"Healed {amount} HP.");
        }

        // ─────────────── Teleport ───────────────

        private void CmdTeleport(string[] args)
        {
            if (args.Length < 3)
            {
                Log("Usage: tp <x> <y> <z>");
                return;
            }

            if (!float.TryParse(args[0], out float x) ||
                !float.TryParse(args[1], out float y) ||
                !float.TryParse(args[2], out float z))
            {
                Log("Invalid coordinates.");
                return;
            }

            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            var pos = new Vector3(x, y, z);
            var motor = player.GetComponent<ThirdPersonMotor>();
            if (motor != null)
            {
                motor.ServerTeleport(pos, player.transform.rotation);
            }
            else
            {
                player.transform.position = pos;
            }
            Log($"Teleported to ({x}, {y}, {z})");
        }

        // ─────────────── Speed ───────────────

        private void CmdSpeed(string[] args)
        {
            if (args.Length == 0)
            {
                Log("Usage: speed <value>");
                return;
            }

            if (!float.TryParse(args[0], out float spd))
            {
                Log("Invalid speed value.");
                return;
            }

            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }

            var motor = player.GetComponent<ThirdPersonMotor>();
            if (motor != null)
            {
                motor.SetMoveSpeed(spd);
                Log($"Move speed set to {spd}");
            }
        }

        // ─────────────── Info Commands ───────────────

        private void CmdListPlayers()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) { Log("Not connected."); return; }

            Log($"=== Players ({nm.ConnectedClientsIds.Count}) ===");
            foreach (var id in nm.ConnectedClientsIds)
            {
                string marker = id == nm.LocalClientId ? " (you)" : "";
                if (nm.ConnectedClients.TryGetValue(id, out var client) && client.PlayerObject != null)
                {
                    var pos = client.PlayerObject.transform.position;
                    Log($"  [{id}]{marker} pos=({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                }
                else
                {
                    Log($"  [{id}]{marker} (no player object)");
                }
            }
        }

        private void CmdScene()
        {
            Log($"Active scene: {SceneManager.GetActiveScene().name}");
        }

        private void CmdPos()
        {
            var player = GetLocalPlayer();
            if (player == null) { Log("No local player found."); return; }
            var p = player.transform.position;
            Log($"Position: ({p.x:F2}, {p.y:F2}, {p.z:F2})");
        }

        // ─────────────────────── Helpers ───────────────────────

        private static Transform GetLocalPlayer()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;

            var localPlayer = nm.LocalClient?.PlayerObject;
            return localPlayer != null ? localPlayer.transform : null;
        }

        private void Log(string message)
        {
            _log.Add(message);
            if (_log.Count > MaxLogLines)
                _log.RemoveAt(0);
            RefreshLog();
        }

        private void RefreshLog()
        {
            if (_logText != null)
                _logText.text = string.Join("\n", _log);

            // Auto-scroll to bottom
            if (_scrollRect != null)
                Canvas.ForceUpdateCanvases();
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }

        // ─────────────────────── UI Construction ───────────────────────

        private void BuildUI()
        {
            // Canvas
            var canvasGo = new GameObject("AdminConsoleCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999; // Above everything
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel — bottom half of screen
            _panel = new GameObject("Panel");
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            var panelRt = _panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0.45f);
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

            // Title bar
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_panel.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "  ADMIN CONSOLE  (` to toggle)";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 16;
            titleText.color = new Color(0.6f, 0.8f, 1f);
            titleText.alignment = TextAnchor.MiddleLeft;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.92f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(8f, 0f);
            titleRt.offsetMax = Vector2.zero;

            // Scroll view for log
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(_panel.transform, false);
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0.02f, 0.02f, 0.04f, 0.8f);
            _scrollRect = scrollGo.AddComponent<ScrollRect>();
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.005f, 0.12f);
            scrollRt.anchorMax = new Vector2(0.995f, 0.91f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;

            // Content for scroll view
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0f, 0f);
            contentRt.offsetMin = new Vector2(6f, 0f);
            contentRt.offsetMax = new Vector2(-6f, 0f);

            _logText = contentGo.AddComponent<Text>();
            _logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _logText.fontSize = 16;
            _logText.color = new Color(0.85f, 0.9f, 0.85f);
            _logText.alignment = TextAnchor.LowerLeft;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow;
            _logText.supportRichText = true;

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = contentRt;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport mask
            var maskGo = scrollGo;
            maskGo.AddComponent<Mask>().showMaskGraphic = true;
            _scrollRect.viewport = scrollRt;

            // Input field — bottom strip
            var inputGo = new GameObject("InputField");
            inputGo.transform.SetParent(_panel.transform, false);
            var inputImg = inputGo.AddComponent<Image>();
            inputImg.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            var inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.005f, 0.01f);
            inputRt.anchorMax = new Vector2(0.995f, 0.10f);
            inputRt.offsetMin = inputRt.offsetMax = Vector2.zero;

            // Prompt ">"
            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(inputGo.transform, false);
            var promptText = promptGo.AddComponent<Text>();
            promptText.text = ">";
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptText.fontSize = 18;
            promptText.color = new Color(0.4f, 1f, 0.4f);
            promptText.alignment = TextAnchor.MiddleLeft;
            var promptRt = promptGo.GetComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0f, 0f);
            promptRt.anchorMax = new Vector2(0.03f, 1f);
            promptRt.offsetMin = new Vector2(8f, 0f);
            promptRt.offsetMax = Vector2.zero;

            // Text child for InputField
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.03f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(4f, 2f);
            textRt.offsetMax = new Vector2(-4f, -2f);

            _inputField = inputGo.AddComponent<InputField>();
            _inputField.textComponent = text;
            _inputField.caretColor = Color.white;
            _inputField.selectionColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
            _inputField.onValueChanged.AddListener(val => _inputText = val);
            _inputField.onEndEdit.AddListener(val =>
            {
                // Re-activate so it doesn't lose focus
                if (_isOpen && _inputField != null)
                {
                    _inputField.ActivateInputField();
                    _inputField.Select();
                }
            });

            Log("Admin Console ready. Type 'help' for commands.");
        }
    }
}
