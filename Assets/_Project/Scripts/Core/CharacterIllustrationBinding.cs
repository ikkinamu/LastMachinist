using UnityEngine;
using LastMachinist.Quest;

namespace LastMachinist.Core
{
    // Scene-owned binding layer: dialogue data carries only CharacterId/SpeakerId;
    // art can be replaced in the inspector without changing quest logic.
    [System.Serializable]
    public sealed class CharacterIllustrationBinding
    {
        public string characterId;
        public Texture2D dialogueDefault;
    }

    public static class CharacterIllustrationResolver
    {
        public static Texture2D Resolve(DialogueNode node, CharacterIllustrationBinding[] bindings)
        {
            if (node == null || bindings == null) return null;
            string id = node.SpeakerId == "park_seongcheol" ? "Seongcheol" :
                        node.SpeakerId == "lee_seoyeon" ? "Seoyeon" : node.SpeakerId;
            for (int i = 0; i < bindings.Length; i++)
                if (bindings[i] != null && bindings[i].characterId == id)
                    return bindings[i].dialogueDefault;
            return null;
        }
    }
}
