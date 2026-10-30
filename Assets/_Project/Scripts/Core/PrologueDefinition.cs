using System;
using LastMachinist.Quest;
using UnityEngine;

namespace LastMachinist.Core
{
    public enum PrologueStatus { NotStarted, InProgress, Completed }

    [Serializable] public sealed class PrologueBeat
    {
        public int Cut;
        public DialogueNode Dialogue;
    }
    [Serializable] public sealed class PrologueSequence
    {
        public PrologueBeat[] Beats;
    }

    [CreateAssetMenu(menuName = "Last Machinist/Prologue and Title Art")]
    public sealed class PrologueDefinition : ScriptableObject
    {
        public Texture2D TitleBackgroundImage;
        public Texture2D PrologueCut01, PrologueCut02, PrologueCut03, PrologueCut04;
        public Texture2D PrologueCut05, PrologueCut06, PrologueCut07, PrologueCut08;
        public TextAsset Sequence;
        public void EnsureExternalSlots()
        {
            TitleBackgroundImage ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut08");
            PrologueCut01 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut01");
            PrologueCut02 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut02");
            PrologueCut03 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut03");
            PrologueCut04 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut04");
            PrologueCut05 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut05");
            PrologueCut06 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut06");
            PrologueCut07 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut07");
            PrologueCut08 ??= Resources.Load<Texture2D>("PrologueArt/PrologueCut08");
        }
        public Texture2D Cut(int index)
        {
            EnsureExternalSlots();
            switch (index)
            {
                case 1: return PrologueCut01; case 2: return PrologueCut02;
                case 3: return PrologueCut03; case 4: return PrologueCut04;
                case 5: return PrologueCut05; case 6: return PrologueCut06;
                case 7: return PrologueCut07; case 8: return PrologueCut08;
                default: return null;
            }
        }
        public PrologueSequence Build() => JsonUtility.FromJson<PrologueSequence>(Sequence.text);
    }

    public sealed class ProloguePlayback
    {
        private readonly PrologueSequence sequence;
        public int Index { get; private set; }
        public bool Completed => Index >= sequence.Beats.Length;
        public PrologueBeat Current => Completed ? null : sequence.Beats[Index];
        public ProloguePlayback(PrologueSequence value) { sequence = value; }
        public bool Advance(string choice = null)
        {
            if (Completed) return false;
            var choices = Current.Dialogue.Choices;
            if (choices != null && choices.Count > 0 && !choices.Exists(x => x.Id == choice)) return false;
            Index++; return true;
        }
    }
}
