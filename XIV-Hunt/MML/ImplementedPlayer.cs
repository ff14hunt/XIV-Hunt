using System;
using TextPlayer.MML;

namespace FFXIV_GameSense.MML
{
    class ImplementedPlayer : MultiTrackMMLPlayer
    {
        public ImplementedPlayer() : base() { }
        protected override void PlayNote(TextPlayer.Note note, int _, TimeSpan time)
        {

        }
    }
}
