using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Pipliz;
using Pipliz.JSON;
using Pipliz.Chatting;

namespace ScarabolMods
{
  public static class ManagerBlueprints
  {
    public static string BLUEPRINTS_PREFIX = ConstructionModEntries.MOD_PREFIX + "blueprints.";
    public static Dictionary<string, List<BlueprintTodoBlock>> blueprints = new Dictionary<string, List<BlueprintTodoBlock>>();

    public static void LoadBlueprints(string blueprintsPath)
    {
      Dictionary<string, string> prefixesBlueprints = new Dictionary<string, string>();
      Dictionary<string, string> prefixesCapsules = new Dictionary<string, string>();
      string[] prefixFiles = Directory.GetFiles(Path.Combine(ConstructionModEntries.AssetsDirectory, "localization"), "prefixes.json", SearchOption.AllDirectories);
      foreach (string filepath in prefixFiles) {
        try {
          JSONNode jsonPrefixes;
          if (Pipliz.JSON.JSON.Deserialize(filepath, out jsonPrefixes, false)) {
            string locName = Directory.GetParent(filepath).Name;
            Pipliz.Log.Write(string.Format("Found prefixes localization file for '{0}' localization", locName));
            string blueprintsPrefix;
            if (jsonPrefixes.TryGetAs<string>("blueprints", out blueprintsPrefix)) {
              prefixesBlueprints[locName] = blueprintsPrefix;
            }
            string capsulesPrefix;
            if (jsonPrefixes.TryGetAs<string>("capsules", out capsulesPrefix)) {
              prefixesCapsules[locName] = capsulesPrefix;
            }
          }
        } catch (Exception exception) {
          Pipliz.Log.WriteError(string.Format("Exception reading localization from {0}; {1}", filepath, exception.Message));
        }
      }
      Pipliz.Log.Write(string.Format("Loading blueprints from {0}", blueprintsPath));
      string[] files = Directory.GetFiles(blueprintsPath, "**.json", SearchOption.AllDirectories);
      foreach (string filepath in files) {
        try {
          JSONNode json;
          if (Pipliz.JSON.JSON.Deserialize(filepath, out json, false)) {
            string filename = Path.GetFileName(filepath);
            string blueprintName = Path.GetFileNameWithoutExtension(filepath).Replace(" ", "_").ToLower();
            int offx = 0;
            int offy = 0;
            int offz = 0;
            Dictionary<string, BlueprintTodoBlock> blocks = new Dictionary<string, BlueprintTodoBlock>();
            JSONNode jsonBlocks;
            if (json.NodeType == NodeType.Object) {
              if (!json.TryGetAs<JSONNode>("blocks", out jsonBlocks)) {
                Pipliz.Log.WriteError(string.Format("Expected 'blocks' key in json {0}", filename));
                continue;
              }
              JSONNode jsonOffset;
              if (json.TryGetAs<JSONNode>("offset", out jsonOffset)) {
                offx = -jsonOffset.GetAs<int>("x");
                offy = -jsonOffset.GetAs<int>("y");
                offz = -jsonOffset.GetAs<int>("z");
              }
              JSONNode jsonLocalization;
              if (json.TryGetAs<JSONNode>("localization", out jsonLocalization)) {
                foreach (KeyValuePair<string, JSONNode> locEntry in jsonLocalization.LoopObject()) {
                  string labelPrefix;
                  string capsulePrefix;
                  if (prefixesBlueprints.TryGetValue(locEntry.Key, out labelPrefix)) {
                    labelPrefix = labelPrefix.Trim();
                  } else {
                    labelPrefix = "Blueprint";
                  }
                  if (prefixesCapsules.TryGetValue(locEntry.Key, out capsulePrefix)) {
                    capsulePrefix = capsulePrefix.Trim();
                  } else {
                    capsulePrefix = "Emperor Capsule";
                  }
                  string label = ((string) locEntry.Value.BareObject).Trim();
                  ModLocalizationHelper.localize(locEntry.Key, "types.json", new JSONNode()
                                                 .SetAs(blueprintName, labelPrefix + " " + label)
                                                 .SetAs(blueprintName + CapsulesModEntries.CAPSULE_SUFFIX, capsulePrefix + " " + label)
                                                   , BLUEPRINTS_PREFIX, false);
                }
              }
            } else {
              jsonBlocks = json; // fallback everything is an array
              Pipliz.Log.Write(string.Format("No json object defined in '{0}', using full content as blocks array", filename));
              int maxx = 0, maxy = 0, maxz = 0;
              foreach (JSONNode node in jsonBlocks.LoopArray()) {
                int x = getJSONInt(node, "startx", "x", 0, false);
                if (x < offx) { offx = x; }
                if (x > maxx) { maxx = x; }
                int y = getJSONInt(node, "starty", "y", 0, false);
                if (y < offy) { offy = y; }
                if (y > maxy) { maxy = y; }
                int z = getJSONInt(node, "startz", "z", 0, false);
                if (z < offz) { offz = z; }
                if (z > maxz) { maxz = z; }
              }
              for (int x = 0 ; x <= -offx + maxx ; x++) { // add auto-clear area
                for (int y = 0 ; y <= -offz + maxy ; y++) {
                  for (int z = 0 ; z <= -offz + maxz ; z++) {
                    blocks[string.Format("{0}?{1}?{2}", x, y, z)] = new BlueprintTodoBlock(x, y, z, "air");
                  }
                }
              }
            }
            BlueprintTodoBlock originBlock = null;
            foreach (JSONNode node in jsonBlocks.LoopArray()) {
              int startx = getJSONInt(node, "startx", "x", 0, false);
              int starty = getJSONInt(node, "starty", "y", 0, false);
              int startz = getJSONInt(node, "startz", "z", 0, false);
              string typename;
              try {
                typename = node["typename"].GetAs<string>();
              } catch (Exception) {
                try {
                  typename = node["t"].GetAs<string>();
                } catch (Exception) {
                  throw new Exception(string.Format("typename not defined or not a string"));
                }
              }
              int width = getJSONInt(node, "width", "w", 1, true);
              int height = getJSONInt(node, "height", "h", 1, true);
              int depth = getJSONInt(node, "depth", "d", 1, true);
              int dx = 1, dy = 1, dz = 1;
              if (width < 0) { dx = -1; }
              if (height < 0) { dy = -1; }
              if (depth < 0) { dz = -1; }
              for (int x = startx; x * dx < (startx + width) * dx; x += dx) {
                for (int y = starty; y * dy < (starty + height) * dy; y += dy) {
                  for (int z = startz; z * dz < (startz + depth) * dz; z += dz) {
                    int absX = x - offx, absY = y - offy, absZ = z - offz;
                    BlueprintTodoBlock b = new BlueprintTodoBlock(absX, absY, absZ, typename);
                    if (absX == 0 && absY == 0 && absZ == -1) { // do not replace the blueprint box itself (yet)
                      originBlock = b;
                    } else {
                      blocks[string.Format("{0}?{1}?{2}", absX, absY, absZ)] = b;
                    }
                  }
                }
              }
            }
            if (originBlock != null) {
              blocks[string.Format("{0}?{1}?{2}", 0, 0, -1)] = originBlock;
            }
            blueprints.Add(BLUEPRINTS_PREFIX + blueprintName, blocks.Values.ToList());
            Pipliz.Log.Write(string.Format("Added blueprint '{0}' with {1} blocks from {2}", BLUEPRINTS_PREFIX + blueprintName, blocks.Count, filename));
          }
        } catch (Exception exception) {
          Pipliz.Log.WriteError(string.Format("Exception while loading from {0}; {1}", filepath, exception.Message));
        }
      }
    }

    private static int getJSONInt(JSONNode node, string name, string alternativeName, int defaultValue, bool optional)
    {
      try {
        return node[name].GetAs<int>();
      } catch (Exception) {
        try {
          return node[alternativeName].GetAs<int>();
        } catch (Exception) {
          if (optional) {
            return defaultValue;
          } else {
            throw new Exception(string.Format("Neither {0} nor {1} defined or not an integer", name, alternativeName));
          }
        }
      }
    }
  }
}
