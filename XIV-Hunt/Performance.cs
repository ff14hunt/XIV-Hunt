using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FFXIV_GameSense
{
    class Performance
    {
        private static readonly List<Note> notes = XIVDB.GameResources.GetPerformanceNotes();
        internal List<Note> Sheet { get; set; }

        public Performance(string i)
        {
            Sheet = new List<Note>();
            foreach (string s in i.Split(','))
            {
                var st = s.Trim();
                st = LeewaySharpFlat(st);
                st = LeewayNote(st);
                if (notes.Exists(x => x.Name.Equals(st)))
                {
                    var t = notes.Single(x => x.Name.Equals(st));
                    Sheet.Add(new Note { Id = t.Id, Name = t.Name, Wait = 500 });
                }
                else if (st.StartsWith("w", StringComparison.OrdinalIgnoreCase) && uint.TryParse(st.Substring(1), out uint duration))
                {
                    Sheet.Last().Wait = duration;
                }
                else if (st.StartsWith("l", StringComparison.OrdinalIgnoreCase) && uint.TryParse(st.Substring(1), out duration))
                {
                    Sheet.Last().Length = duration;
                }
            }
        }

        private static string LeewaySharpFlat(string nn)
        {
            nn = nn.Replace('#', '♯');
            if (nn.Length > 1 && nn[1] == 'b')
                nn = nn.ReplaceAt(1, '♭');
            return nn;
        }

        private static string LeewayNote(string note)
        {
            if (notes.Exists(x => x.Name == note) || note.Length < 2)
                return note;
            string nn = null;
            if (note[1] == '♭')
            {
                nn = note.Replace('♭', '♯');
                nn = (note[0] == 'A') ? nn.ReplaceAt(0, 'G') : nn.ReplaceAt(0, nn[0].Decrement());
            }
            else if (note[1] == '♯')
            {
                nn = note.Replace('♯', '♭');
                nn = (note[0] == 'G') ? nn.ReplaceAt(0, 'A') : nn.ReplaceAt(0, nn[0].Increment());
            }
            if (notes.Exists(x => x.Name == nn))
                return nn;
            else if (nn != null)
            {
                nn = nn.Remove(1, 1);
                if (notes.Exists(x => x.Name == nn))
                    return nn;
            }
            return note;
        }

        internal async Task PlayAsync(int pid, CancellationToken cts)
        {
            PipeMessage noteOff = new PipeMessage(pid, PMCommand.PlayNote) { Parameter = 0 };
            foreach (Note n in Sheet)
            {
                PersistentNamedPipeServer.SendPipeMessage(new PipeMessage(pid, PMCommand.PlayNote) { Parameter = n.Id });
                await Task.Delay((int)n.Length);
                PersistentNamedPipeServer.SendPipeMessage(noteOff);
                TimeSpan untilNextNote = TimeSpan.FromMilliseconds((int)n.Wait - (int)n.Length);
                if(untilNextNote.TotalMilliseconds > 0)
                    await Task.Delay(untilNextNote);
                if (cts.IsCancellationRequested)
                    break;
            }
        }
    }

    class Note
    {
        public byte Id { get; set; }
        public string Name { get; set; }
        public uint Wait { get; set; }
        public uint Length { get; set; }
    }
}