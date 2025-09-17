using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using Blasphemous.Randomizer.EnemyRando;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using HarmonyLib;
using Framework.Audio;
using System;

[BepInPlugin("com.yourname.blasphemous.tikfinity", "Blasphemous Tikfinity Mod", "1.6.0")]
public class TikfinityMod : BaseUnityPlugin
{
    // Singleton para accesibilidad desde los patches
    public static TikfinityMod Instance { get; private set; }

    internal ManualLogSource logger;
    private ConfigEntry<KeyCode> debugKey;
    private ConfigEntry<bool> forceSpawn;

    // Per-key configurable press counts
    private ConfigEntry<int> alpha1PressCount;
    private ConfigEntry<int> alpha2PressCount;
    private ConfigEntry<int> alpha3PressCount;
    private ConfigEntry<int> alpha4PressCount;
    private ConfigEntry<int> alpha5PressCount;
    private ConfigEntry<int> alpha6PressCount;
    private ConfigEntry<int> alpha7PressCount;
    private ConfigEntry<int> alpha8PressCount;
    private ConfigEntry<int> alpha9PressCount;
    private ConfigEntry<int> alpha0PressCount;

    // Track current press counts and last press time per key
    private Dictionary<KeyCode, int> currentPressCounts = new Dictionary<KeyCode, int>();
    private Dictionary<KeyCode, DateTime> lastPressTimes = new Dictionary<KeyCode, DateTime>();

    // Time window for consecutive presses (in seconds)
    private const double PRESS_TIME_WINDOW = 1.5;

    internal bool catalogsInitialized = false;

    // All audio catalogs loaded from Resources
    private FMODAudioCatalog[] allCatalogs;

    // Key -> enemy mapping and Key -> config mapping
    private Dictionary<KeyCode, string> keyToEnemy;
    private Dictionary<KeyCode, ConfigEntry<int>> keyToConfig;

    void Awake()
    {
        Instance = this;
        logger = Logger;
        logger.LogInfo("Tikfinity Mod v1.6.0 loading...");

        debugKey = Config.Bind("Testing", "DebugKey", KeyCode.Y, "Key to list all objects in scene");
        forceSpawn = Config.Bind("Settings", "ForceSpawn", true, "Allow spawning even in safe zones without enemies");

        // Configurable press counts for each key
        alpha1PressCount = Config.Bind("KeyBindings", "Alpha1PressCount", 1, "Number of '1' key presses needed to spawn EV11");
        alpha2PressCount = Config.Bind("KeyBindings", "Alpha2PressCount", 1, "Number of '2' key presses needed to spawn EN22");
        alpha3PressCount = Config.Bind("KeyBindings", "Alpha3PressCount", 1, "Number of '3' key presses needed to spawn EN20");
        alpha4PressCount = Config.Bind("KeyBindings", "Alpha4PressCount", 1, "Number of '4' key presses needed to spawn EN01");
        alpha5PressCount = Config.Bind("KeyBindings", "Alpha5PressCount", 1, "Number of '5' key presses needed to spawn EN26");
        alpha6PressCount = Config.Bind("KeyBindings", "Alpha6PressCount", 1, "Number of '6' key presses needed to spawn EV03");
        alpha7PressCount = Config.Bind("KeyBindings", "Alpha7PressCount", 1, "Number of '7' key presses needed to spawn EN27");
        alpha8PressCount = Config.Bind("KeyBindings", "Alpha8PressCount", 1, "Number of '8' key presses needed to spawn EN16");
        alpha9PressCount = Config.Bind("KeyBindings", "Alpha9PressCount", 1, "Number of '9' key presses needed to spawn EN03");
        alpha0PressCount = Config.Bind("KeyBindings", "Alpha0PressCount", 1, "Number of '0' key presses needed to spawn EV26");

        // Build maps
        keyToEnemy = new Dictionary<KeyCode, string>()
        {
            { KeyCode.Alpha1, "EV11" }, // press 1 -> EV11
            { KeyCode.Alpha2, "EN22" }, // press 1 -> EN22
            { KeyCode.Alpha3, "EN20" }, // press 1 -> EN20
            { KeyCode.Alpha4, "EN01" }, // press 1 -> EN01
            { KeyCode.Alpha5, "EN26" }, // press 1 -> EN26
            { KeyCode.Alpha6, "EV03" }, // press 1 -> EV03
            { KeyCode.Alpha7, "EN27" }, // press 1 -> EN27
            { KeyCode.Alpha8, "EN16" }, // press 1 -> EN16
            { KeyCode.Alpha9, "EN03" }, // press 1 -> EN03
            { KeyCode.Alpha0, "EV26" }  // press 1 -> EV26
        };

        keyToConfig = new Dictionary<KeyCode, ConfigEntry<int>>()
        {
            { KeyCode.Alpha1, alpha1PressCount },
            { KeyCode.Alpha2, alpha2PressCount },
            { KeyCode.Alpha3, alpha3PressCount },
            { KeyCode.Alpha4, alpha4PressCount },
            { KeyCode.Alpha5, alpha5PressCount },
            { KeyCode.Alpha6, alpha6PressCount },
            { KeyCode.Alpha7, alpha7PressCount },
            { KeyCode.Alpha8, alpha8PressCount },
            { KeyCode.Alpha9, alpha9PressCount },
            { KeyCode.Alpha0, alpha0PressCount }
        };

        // Initialize per-key counters
        foreach (var key in keyToEnemy.Keys)
        {
            currentPressCounts[key] = 0;
            lastPressTimes[key] = DateTime.MinValue;
        }

        // Initialize audio catalogs
        StartCoroutine(InitializeAudioCatalogs());

        // Inicializar Harmony y aplicar todos los patches
        var harmony = new Harmony("com.yourname.blasphemous.tikfinity");
        harmony.PatchAll();

        logger.LogInfo("Tikfinity Mod initialized with Harmony patches!");
    }

    void Update()
    {
        // Detect '¿' key press (robust: check Input.inputString for '¿' and fallback to Backslash+Shift)
        try
        {
            if (!string.IsNullOrEmpty(Input.inputString) && Input.inputString.Contains("¿"))
            {
                logger.LogInfo("Detected '¿' via Input.inputString - attempting instant kill.");
                KillPlayerInstant();
            }
            else if (Input.GetKeyDown(KeyCode.Backslash) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                // Many keyboards produce '¿' as Shift + Backslash
                logger.LogInfo("Detected Backslash + Shift - attempting instant kill.");
                KillPlayerInstant();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Error while checking for '¿' key: " + ex.Message);
        }

        // Check all mapped keys
        foreach (var kv in keyToEnemy)
        {
            if (Input.GetKeyDown(kv.Key))
            {
                HandleKeyPress(kv.Key);
            }
        }

        if (Input.GetKeyDown(debugKey.Value))
            DumpEnemyIds();
    }

    void HandleKeyPress(KeyCode key)
    {
        if (!keyToEnemy.ContainsKey(key))
            return;

        string enemyId = keyToEnemy[key];
        int requiredPresses = keyToConfig.ContainsKey(key) ? keyToConfig[key].Value : 1;

        DateTime now = DateTime.Now;

        // Reset counter if too much time has passed since last press
        if ((now - lastPressTimes[key]).TotalSeconds > PRESS_TIME_WINDOW)
        {
            currentPressCounts[key] = 0;
        }

        currentPressCounts[key]++;
        lastPressTimes[key] = now;

        logger.LogInfo($"Key {key} pressed for {enemyId}. Current: {currentPressCounts[key]}/{requiredPresses}");

        if (currentPressCounts[key] >= requiredPresses)
        {
            logger.LogInfo($"Threshold reached for {enemyId}! Spawning enemy...");
            SpawnEnemy(enemyId);
            currentPressCounts[key] = 0;
        }
    }

    internal IEnumerator InitializeAudioCatalogs()
    {
        // Wait for AudioLoader to be fully initialized
        AudioLoader loader = FindObjectOfType<AudioLoader>();
        while (loader == null || loader.AudioCatalogs == null)
        {
            yield return new WaitForSeconds(0.1f);
            loader = FindObjectOfType<AudioLoader>();
        }

        // Load all audio catalogs from resources
        allCatalogs = Resources.FindObjectsOfTypeAll<FMODAudioCatalog>();

        // Create combined list avoiding duplicates
        var combinedCatalogs = new List<FMODAudioCatalog>(loader.AudioCatalogs);

        foreach (var catalog in allCatalogs)
        {
            if (!combinedCatalogs.Any(c => c.name == catalog.name))
            {
                combinedCatalogs.Add(catalog);
            }
        }

        // Set back to loader
        loader.AudioCatalogs = combinedCatalogs.ToArray();
        catalogsInitialized = true;

        logger.LogInfo($"Audio catalogs initialized. Total: {combinedCatalogs.Count}");
    }

    bool CheckAudioEventExists(string eventName)
    {
        var loader = FindObjectOfType<AudioLoader>();
        if (loader == null || loader.AudioCatalogs == null)
            return false;

        foreach (var catalog in loader.AudioCatalogs)
        {
            FieldInfo itemsField = typeof(FMODAudioCatalog).GetField("audioItems",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (itemsField != null)
            {
                var items = itemsField.GetValue(catalog) as IEnumerable;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        PropertyInfo keyProp = item.GetType().GetProperty("key");
                        if (keyProp != null)
                        {
                            object keyValue = keyProp.GetValue(item, null);
                            if (keyValue != null && keyValue.ToString() == eventName)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    void SpawnEnemy(string enemyIdToSpawn)
    {
        // Audio check
        if (!CheckAudioEventExists("FlagellantFootStep"))
        {
            logger.LogWarning("FlagellantFootStep audio event not found! Attempting to reload catalogs...");
            StartCoroutine(InitializeAudioCatalogs());
        }

        EnemyLoader.loadEnemies();

        Vector3 spawnPos = GetPlayerRelativePosition();
        logger.LogInfo($"Attempting to spawn at {spawnPos} using ID {enemyIdToSpawn}");

        GameObject prefab = null;

        FieldInfo field = typeof(EnemyLoader).GetField("allEnemies", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            var dict = field.GetValue(null) as Dictionary<string, GameObject>;
            if (dict != null && dict.ContainsKey(enemyIdToSpawn))
                prefab = dict[enemyIdToSpawn];
        }

        if (prefab != null)
        {
            logger.LogInfo($"Got prefab: {prefab.name}");
            GameObject newEnemy = Instantiate(prefab, spawnPos, Quaternion.identity);
            newEnemy.name = $"TikfinitySpawned_{enemyIdToSpawn}_{DateTime.Now.Ticks}";

            // Force facing left ONLY for EN16 and EV26
            if (enemyIdToSpawn == "EN16" || enemyIdToSpawn == "EV26")
            {
                Vector3 scale = newEnemy.transform.localScale;
                if (scale.x > 0) scale.x *= -1; // flip only if currently facing right
                newEnemy.transform.localScale = scale;

                logger.LogInfo($"{enemyIdToSpawn} forced to face left");
            }

            // Ensure the enemy is active and components are enabled
            newEnemy.SetActive(true);
            InitializeSpawnedEnemy(newEnemy);

            logger.LogInfo($"Spawned enemy {enemyIdToSpawn} at {spawnPos}");
        }
        else
        {
            logger.LogWarning($"Enemy {enemyIdToSpawn} not found, spawning placeholder.");
            CreatePlaceholderEnemy(spawnPos);
        }
    }

    Vector3 GetPlayerRelativePosition()
    {
        GameObject player = FindPlayer();
        if (player != null)
        {
            Vector3 playerPos = player.transform.position;
            bool facingLeft = false;

            // Method 1: Check player's sprite renderer with multiple approaches
            var spriteRenderer = player.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                // Try both flipX and scale-based detection
                bool flipXLeft = spriteRenderer.flipX;
                bool scaleLeft = spriteRenderer.transform.localScale.x < 0;

                logger.LogInfo($"SpriteRenderer.flipX: {flipXLeft}, Scale.x < 0: {scaleLeft}");

                // Sometimes flipX logic is inverted, let's try both interpretations
                facingLeft = flipXLeft;
                logger.LogInfo($"Initial detection from flipX: {(facingLeft ? "Left" : "Right")}");
            }

            // Method 2: Try to access Penitent-specific components for facing direction
            try
            {
                var penitent = player.GetComponent<Gameplay.GameControllers.Penitent.Penitent>();
                if (penitent != null)
                {
                    // Try to access orientation through reflection
                    var statusField = penitent.GetType().GetField("Status", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (statusField != null)
                    {
                        var status = statusField.GetValue(penitent);
                        if (status != null)
                        {
                            var orientationProp = status.GetType().GetProperty("Orientation");
                            if (orientationProp != null)
                            {
                                // Replace this line:
                                // var orientation = orientationProp.GetValue(status);

                                // With this line:
                                var orientation = orientationProp.GetValue(status, null);
                                logger.LogInfo($"Penitent orientation: {orientation}");

                                // Common orientation values: 0 = right, 1 = left, or similar
                                if (orientation != null)
                                {
                                    int orientationValue = Convert.ToInt32(orientation);
                                    facingLeft = orientationValue == 1; // Adjust this based on what values you see in logs
                                    logger.LogInfo($"Detected facing from Penitent orientation: {(facingLeft ? "Left" : "Right")}");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger.LogInfo($"Could not access Penitent orientation: {ex.Message}");
            }

            // Method 3: Check transform scale as fallback
            if (spriteRenderer == null)
            {
                facingLeft = player.transform.localScale.x < 0;
                logger.LogInfo($"Detected facing from Scale: {(facingLeft ? "Left" : "Right")}");
            }

            // Method 4: Try velocity-based detection as additional check
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null && Mathf.Abs(rb.velocity.x) > 0.1f)
            {
                bool movingLeft = rb.velocity.x < 0;
                logger.LogInfo($"Player velocity indicates moving: {(movingLeft ? "Left" : "Right")} (velocity.x: {rb.velocity.x})");

                // Only override facing if we're actually moving
                if (Mathf.Abs(rb.velocity.x) > 0.5f)
                {
                    facingLeft = movingLeft;
                    logger.LogInfo($"Overriding facing direction based on movement: {(facingLeft ? "Left" : "Right")}");
                }
            }

            // Spawn enemy in the direction the player is facing
            float xOffset = facingLeft ? -3f : 3f; // Increased distance slightly
            Vector3 spawnPos = playerPos + new Vector3(xOffset, 3f, 0f);

            logger.LogInfo($"FINAL: Player position: {playerPos}, Spawn position: {spawnPos}, Facing left: {facingLeft}");
            return spawnPos;
        }

        logger.LogWarning("Player not found, defaulting spawn to (0,0,0).");
        return Vector3.zero;
    }


    GameObject FindPlayer()
    {
        // First try to find the player by the most common methods
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) return player;

        // Try specific names used in Blasphemous
        string[] names = { "Penitent", "PenitentOne", "Penitent One", "PENITENT" };
        foreach (string n in names)
        {
            player = GameObject.Find(n);
            if (player != null) return player;
        }

        // Try to find by component
        var comp = FindObjectOfType<Gameplay.GameControllers.Penitent.Penitent>();
        if (comp != null) return comp.gameObject;

        // Last resort: search all objects for something that looks like the player
        var allObjects = FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.activeInHierarchy &&
                (obj.name.ToLower().Contains("player") || obj.name.ToLower().Contains("penitent")))
            {
                return obj;
            }
        }

        logger.LogWarning("Player not found by any method!");
        return null;
    }

    void InitializeSpawnedEnemy(GameObject enemy)
    {
        // Enable all components (simplified approach)
        foreach (var comp in enemy.GetComponentsInChildren<Behaviour>(true))
        {
            comp.enabled = true;
            comp.gameObject.SetActive(true);
        }

        // Try to find and enable any audio-related components by name
        foreach (Transform child in enemy.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.ToLower().Contains("audio") || child.name.ToLower().Contains("sound"))
            {
                child.gameObject.SetActive(true);
            }
        }
    }

    void CreatePlaceholderEnemy(Vector3 pos)
    {
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.transform.position = pos;
        placeholder.transform.localScale = Vector3.one * 2f;
        placeholder.name = "TikfinityPlaceholder";
        var r = placeholder.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.red;
        Destroy(placeholder, 10f);
    }

    void DumpEnemyIds()
    {
        EnemyLoader.loadEnemies();

        FieldInfo field = typeof(EnemyLoader).GetField("allEnemies", BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            var dict = field.GetValue(null) as Dictionary<string, GameObject>;
            if (dict != null)
            {
                logger.LogInfo($"Total enemies loaded: {dict.Count}");
                foreach (var kvp in dict.Take(30))
                    logger.LogInfo($"  {kvp.Key}");
            }
        }
    }

    // ---------------------------
    // NEW: Instant kill routine
    // ---------------------------
    // Replace the existing KillPlayerInstant() and ReenablePlayerNextFrame() with this block:

    void KillPlayerInstant()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            logger.LogWarning("KillPlayerInstant: Player not found - cannot kill.");
            return;
        }

        bool success = false;

        // 1) Zero common fields/properties (explicit names + generic scan)
        try
        {
            var comps = player.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp == null) continue;
                Type t = comp.GetType();

                // Try explicit-known property names first (based on your logs)
                TrySetMemberIfExists(t, comp, "CurrentLife", 0);
                TrySetMemberIfExists(t, comp, "CurrentFervour", 0);
                TrySetMemberIfExists(t, comp, "CurrentCriticalChance", 0);
                TrySetMemberIfExists(t, comp, "CurrentOutputDamage", 0);

                // Generic scan for health-like members
                try
                {
                    var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var f in fields)
                    {
                        string fname = f.Name.ToLower();
                        if (fname.Contains("health") || fname.Contains("life") || fname.Contains("hp") || fname.Contains("current"))
                        {
                            if (f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(double))
                            {
                                f.SetValue(comp, Convert.ChangeType(0, f.FieldType));
                                logger.LogInfo($"KillPlayerInstant: Set field '{t.FullName}.{f.Name}' to 0.");
                                success = true;
                            }
                        }
                    }

                    var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var p in props)
                    {
                        string pname = p.Name.ToLower();
                        if (!p.CanWrite) continue;
                        if (pname.Contains("health") || pname.Contains("life") || pname.Contains("hp") || pname.Contains("current"))
                        {
                            if (p.PropertyType == typeof(int) || p.PropertyType == typeof(float) || p.PropertyType == typeof(double))
                            {
                                p.SetValue(comp, Convert.ChangeType(0, p.PropertyType), null);
                                logger.LogInfo($"KillPlayerInstant: Set property '{t.FullName}.{p.Name}' to 0.");
                                success = true;
                            }
                        }
                    }
                }
                catch (Exception exField)
                {
                    logger.LogInfo($"KillPlayerInstant: while zeroing members on {t.FullName}: {exField.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInfo("KillPlayerInstant: error during zeroing phase: " + ex.Message);
        }

        // 2) Invoke Penitent-specific death/damage APIs if present (stronger shot)
        try
        {
            var penitentComp = player.GetComponent("Gameplay.GameControllers.Penitent.Penitent");
            if (penitentComp == null)
            {
                // Try typed access if the type is available
                var penitentTyped = player.GetComponent<Gameplay.GameControllers.Penitent.Penitent>();
                if (penitentTyped != null) penitentComp = penitentTyped;
            }

            if (penitentComp != null)
            {
                Type pt = penitentComp.GetType();
                string[] tryMethods = new string[] {
                "Die","Death","OnDeath","OnDie","Kill","KillEntity","ReceiveDamage","TakeDamage","Damage","Hit"
            };

                foreach (var mname in tryMethods)
                {
                    try
                    {
                        var m = pt.GetMethod(mname, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (m != null)
                        {
                            var pars = m.GetParameters();
                            if (pars.Length == 0)
                            {
                                m.Invoke(penitentComp, null);
                                logger.LogInfo($"KillPlayerInstant: Invoked '{pt.FullName}.{m.Name}()'.");
                                success = true;
                                break;
                            }
                            else if (pars.Length == 1 && (pars[0].ParameterType == typeof(int) || pars[0].ParameterType == typeof(float) || pars[0].ParameterType == typeof(double)))
                            {
                                object big = pars[0].ParameterType == typeof(int) ? (object)int.MaxValue : (object)float.MaxValue;
                                m.Invoke(penitentComp, new object[] { big });
                                logger.LogInfo($"KillPlayerInstant: Invoked '{pt.FullName}.{m.Name}({big})'.");
                                success = true;
                                break;
                            }
                        }
                    }
                    catch { /* swallow single-method exceptions and try next */ }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInfo("KillPlayerInstant: while invoking penitent methods: " + ex.Message);
        }

        // 3) Try to set CHERUB_RESPAWN flag (search for any static SetFlag-like method)
        try
        {
            bool flagSet = TryInvokeStaticStringBoolMethod("setflag", "CHERUB_RESPAWN", true);
            if (flagSet) { logger.LogInfo("KillPlayerInstant: Set CHERUB_RESPAWN via discovered API."); success = true; }
        }
        catch (Exception ex) { logger.LogInfo("KillPlayerInstant: setflag attempt failed: " + ex.Message); }

        // 4) Try to raise "ENTITY_DEAD" event via any Raise/Send/Trigger event-like API found
        try
        {
            bool raised = TryInvokeStaticEventByName("ENTITY_DEAD");
            if (raised) { logger.LogInfo("KillPlayerInstant: Raised ENTITY_DEAD via discovered API."); success = true; }
        }
        catch (Exception ex) { logger.LogInfo("KillPlayerInstant: raise event attempt failed: " + ex.Message); }

        // 5) Try to set the global game state to PLAYERDEAD (look for GameManager-like types)
        try
        {
            bool stateSet = TrySetGameStateTo("PLAYERDEAD");
            if (stateSet) { logger.LogInfo("KillPlayerInstant: Set game state to PLAYERDEAD via discovered API."); success = true; }
        }
        catch (Exception ex) { logger.LogInfo("KillPlayerInstant: set game state attempt failed: " + ex.Message); }

        // 6) Animator triggers
        try
        {
            var animator = player.GetComponent<Animator>();
            if (animator != null)
            {
                string[] triggers = { "death", "die", "Death", "Die" };
                foreach (var trig in triggers)
                {
                    try { animator.SetTrigger(trig); logger.LogInfo($"KillPlayerInstant: Set animator trigger '{trig}'."); success = true; }
                    catch { }
                }
            }
        }
        catch (Exception ex) { logger.LogInfo("KillPlayerInstant: animator attempt failed: " + ex.Message); }

        // 7) Last resort: deactivate / reactivate player
        if (!success)
        {
            try
            {
                logger.LogWarning("KillPlayerInstant: could not find health/die APIs or events. Deactivating player GameObject as last resort.");
                player.SetActive(false);
                StartCoroutine(ReenablePlayerNextFrame(player));
                success = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning("KillPlayerInstant: final fallback failed: " + ex.Message);
            }
        }

        logger.LogInfo("KillPlayerInstant completed. Success=" + success);
    }

    IEnumerator ReenablePlayerNextFrame(GameObject player)
    {
        yield return null;
        if (player != null)
        {
            player.SetActive(true);
            logger.LogInfo("Reenabled player GameObject after temporary deactivate.");
        }
    }

    // ----------------- Helper reflection methods -----------------

    void TrySetMemberIfExists(Type t, object instance, string name, object value)
    {
        try
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(double)))
            {
                f.SetValue(instance, Convert.ChangeType(value, f.FieldType));
                logger.LogInfo($"KillPlayerInstant: Set field '{t.FullName}.{f.Name}' to {value}.");
                return;
            }

            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && (p.PropertyType == typeof(int) || p.PropertyType == typeof(float) || p.PropertyType == typeof(double)))
            {
                p.SetValue(instance, Convert.ChangeType(value, p.PropertyType), null);
                logger.LogInfo($"KillPlayerInstant: Set property '{t.FullName}.{p.Name}' to {value}.");
                return;
            }
        }
        catch (Exception ex) { logger.LogInfo("TrySetMemberIfExists error: " + ex.Message); }
    }

    bool TryInvokeStaticStringBoolMethod(string methodNameSubstring, string argString, bool argBool)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var type in types)
            {
                try
                {
                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name.ToLower().Contains(methodNameSubstring.ToLower()));
                    foreach (var m in methods)
                    {
                        var pars = m.GetParameters();
                        if (pars.Length == 2 && pars[0].ParameterType == typeof(string) && pars[1].ParameterType == typeof(bool))
                        {
                            try
                            {
                                m.Invoke(null, new object[] { argString, argBool });
                                logger.LogInfo($"TryInvokeStaticStringBoolMethod: Invoked {type.FullName}.{m.Name}('{argString}', {argBool})");
                                return true;
                            }
                            catch { }
                        }
                    }
                }
                catch { /* ignore type errors */ }
            }
        }
        return false;
    }

    bool TryInvokeStaticEventByName(string eventName)
    {
        string[] methodCandidates = new[] { "Raise", "RaiseEvent", "SendEvent", "TriggerEvent", "RaiseSignal", "SendMessageToGame" };

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var type in types)
            {
                try
                {
                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => methodCandidates.Any(c => m.Name.ToLower().Contains(c.ToLower())));
                    foreach (var m in methods)
                    {
                        var pars = m.GetParameters();
                        try
                        {
                            if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                            {
                                m.Invoke(null, new object[] { eventName });
                                logger.LogInfo($"TryInvokeStaticEventByName: Invoked {type.FullName}.{m.Name}('{eventName}')");
                                return true;
                            }
                            else if (pars.Length == 2 && pars[0].ParameterType == typeof(string))
                            {
                                // try passing a null/empty second param
                                m.Invoke(null, new object[] { eventName, null });
                                logger.LogInfo($"TryInvokeStaticEventByName: Invoked {type.FullName}.{m.Name}('{eventName}', null)");
                                return true;
                            }
                        }
                        catch { /* ignore invocation errors */ }
                    }
                }
                catch { /* ignore type iteration errors */ }
            }
        }
        return false;
    }

    bool TrySetGameStateTo(string targetStateName)
    {
        // Attempt #1: find enum values that match targetStateName and try to set any static GameState property
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var type in types)
            {
                try
                {
                    // if type is enum, check values
                    if (type.IsEnum)
                    {
                        foreach (var name in Enum.GetNames(type))
                        {
                            if (string.Equals(name, targetStateName, StringComparison.OrdinalIgnoreCase))
                            {
                                object enumVal = Enum.Parse(type, name);

                                // find GameManager-like types with a static SetState or State property that accepts this enum
                                foreach (var t2 in types)
                                {
                                    try
                                    {
                                        var setMethod = t2.GetMethod("SetState", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                        if (setMethod != null)
                                        {
                                            var pars = setMethod.GetParameters();
                                            if (pars.Length == 1 && pars[0].ParameterType == type)
                                            {
                                                setMethod.Invoke(null, new object[] { enumVal });
                                                logger.LogInfo($"TrySetGameStateTo: Invoked {t2.FullName}.{setMethod.Name}({targetStateName})");
                                                return true;
                                            }
                                        }

                                        var prop = t2.GetProperty("State", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                                        if (prop != null && prop.PropertyType == type && prop.CanWrite)
                                        {
                                            prop.SetValue(null, enumVal, null);
                                            logger.LogInfo($"TrySetGameStateTo: Set static property {t2.FullName}.State = {targetStateName}");
                                            return true;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        // Attempt #2: find any static SetState(string) or ChangeState(string)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types = null;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var type in types)
            {
                try
                {
                    var m = type.GetMethod("SetState", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);
                    if (m != null)
                    {
                        try { m.Invoke(null, new object[] { targetStateName }); logger.LogInfo($"TrySetGameStateTo: Invoked {type.FullName}.SetState(\"{targetStateName}\")"); return true; }
                        catch { }
                    }
                    var m2 = type.GetMethod("ChangeState", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);
                    if (m2 != null)
                    {
                        try { m2.Invoke(null, new object[] { targetStateName }); logger.LogInfo($"TrySetGameStateTo: Invoked {type.FullName}.ChangeState(\"{targetStateName}\")"); return true; }
                        catch { }
                    }
                }
                catch { }
            }
        }

        return false;
    }

}

// Harmony patch to ensure audio catalogs are properly initialized
[HarmonyPatch(typeof(AudioLoader), "Start")]
public static class AudioLoader_Patch
{
    static void Postfix(AudioLoader __instance)
    {
        TikfinityMod.Instance?.logger.LogInfo("AudioLoader started, initializing catalogs...");
        TikfinityMod.Instance?.StartCoroutine(TikfinityMod.Instance.InitializeAudioCatalogs());
    }
}

[HarmonyPatch(typeof(EnemyLoader), "loadEnemies")]
public static class EnemyLoader_Patch
{
    static void Postfix()
    {
        // Ensure audio catalogs are available when enemies load
        if (!TikfinityMod.Instance.catalogsInitialized)
        {
            TikfinityMod.Instance?.logger.LogInfo("Enemies loaded, initializing catalogs...");
            TikfinityMod.Instance?.StartCoroutine(TikfinityMod.Instance.InitializeAudioCatalogs());
        }
    }
}
