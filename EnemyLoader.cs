using Blasphemous.ModdingAPI;
using Gameplay.GameControllers.Entities;
using System.Collections.Generic;
using UnityEngine;

namespace Blasphemous.Randomizer.EnemyRando
{
    public static class EnemyLoader
    {
        private static Dictionary<string, GameObject> allEnemies = new Dictionary<string, GameObject>();
        public static bool loaded = false;

        // Get the gameobject for a certain enemy id
        public static GameObject getEnemy(string id, bool facingLeft)
        {
            if (id == "EN11" || id == "EV15")
            {
                id += facingLeft ? "_L" : "_R";
            }
            if (allEnemies.ContainsKey(id))
            {
                return allEnemies[id];
            }
            return null;
        }

        // Every scene, try to load more enemies
        public static void loadEnemies()
        {
            if (loaded)
                return;

            Enemy[] array = Resources.FindObjectsOfTypeAll<Enemy>();

            for (int i = 0; i < array.Length; i++)
            {
                string baseId = array[i].Id;
                string fullId = baseId;

                // Load separate objects for left/right wall enemies
                if (baseId == "EN11" || baseId == "EV15")
                {
                    if (array[i].name.EndsWith("_L"))
                        fullId += "_L";
                    else if (array[i].name.EndsWith("_R"))
                        fullId += "_R";
                }
                // Load separate objects for normal/exploding heads
                if (baseId == "EN09")
                {
                    if (array[i].name.Contains("_Exploding"))
                    {
                        baseId += "_E";
                        fullId += "_E";
                    }
                }

                // Only filter out invalid stuff
                if (baseId != "" && array[i].gameObject.scene.name == null && !allEnemies.ContainsKey(fullId))
                {
                    changeHitbox(array[i].transform, baseId);

                    if (baseId == "EV27")
                        allEnemies.Add(fullId, array[i].transform.parent.gameObject);
                    else
                        allEnemies.Add(fullId, array[i].gameObject);

                    // Debug log so you can see everything
                    ModLog.Info($"[EnemyLoader] Cached enemy prefab: baseId={baseId}, fullId={fullId}, name={array[i].name}");
                }
            }

            loaded = true;
            ModLog.Info($"[EnemyLoader] Finished loading. Total cached enemies: {allEnemies.Count}");
        }

        // Certain large enemies need a modified hitbox
        private static void changeHitbox(Transform transform, string id)
        {
            if (id == "EN16" || id == "EV23")
            {
                Transform child = transform.Find("#Constitution/Canopy");
                if (child != null)
                {
                    BoxCollider2D component = child.GetComponent<BoxCollider2D>();
                    component.offset = new Vector2(component.offset.x, 1.5f);
                    component.size = new Vector2(component.size.x, 3f);
                    return;
                }
                ModLog.Error("Enemy " + id + " had no hitbox to change!");
            }
            else if (id == "EN15" || id == "EV19" || id == "EV26")
            {
                Transform child = transform.Find("#Constitution/Sprite");
                if (child != null)
                {
                    BoxCollider2D component2 = child.GetComponent<BoxCollider2D>();
                    component2.offset = new Vector2(component2.offset.x, 2f);
                    component2.size = new Vector2(component2.size.x, 3.75f);
                    return;
                }
                ModLog.Error("Enemy " + id + " had no hitbox to change!");
            }
        }
    }
}
