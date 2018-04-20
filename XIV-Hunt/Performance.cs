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
                else if (st.StartsWith("w", StringComparison.CurrentCultureIgnoreCase) && uint.TryParse(st.Substring(1), out uint waitduration))
                {
                    Sheet.Last().Wait = waitduration;
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
            foreach (Note n in Sheet)
            {
                PersistentNamedPipeServer.SendPipeMessage(new PipeMessage(pid, PMCommand.PlayNote) { Parameter = n.Id });
                await Task.Delay((int)n.Wait);
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
    }

    //class MML
    //{
    //    private static readonly char[] basicnotes = { 'A', 'B', 'C', 'D', 'E', 'F', 'G' };
    //    private static readonly Dictionary<byte, string> notes = new Dictionary<byte, string>
    //    {
    //        { 0,"C" },
    //        { 1,"C#" },
    //        { 2,"D" },
    //        { 3,"D#" },
    //        { 4,"E" },
    //        { 5,"F" },
    //        { 6,"F#" },
    //        { 7,"G" },
    //        { 8,"G#" },
    //        { 9,"A" },
    //        { 10,"A#" },
    //        { 11,"B" }
    //    };
    //    List<MMLCommand> Sheet { get; set; }

    //    public MML(string c)
    //    {
    //        Sheet = new List<MMLCommand>();
    //        byte currTempo = 120;
    //        byte currLength = 4;
    //        bool increaseByHalf = false;
    //        byte currOctave = 3;
    //        c = c.Trim().Replace(" ", string.Empty)+"  ";
    //        for (int i = 0; i < c.Length; i++)
    //        {
    //            char currC = char.ToUpperInvariant(c[i]);
    //            Debug.WriteLine(i + ":" + currC);
    //            //Tempo
    //            if (currC == 'T' && char.IsDigit(c[i + 1]))
    //            {
    //                string num = new string(c.Skip(i + 1).TakeWhile(char.IsDigit).ToArray());
    //                currTempo = byte.Parse(num);
    //                i += num.Length;
    //                Debug.WriteLine("Tempo set to: " + currTempo);
    //            }
    //            //Length
    //            else if (currC == 'L' && char.IsDigit(c[i + 1]))
    //            {
    //                string findlength = c.Substring(i + 1, byte.MaxValue.ToString().Length + 1);
    //                string num = new string(findlength.TakeWhile(char.IsDigit).ToArray());
    //                currLength = byte.Parse(num);
    //                if (findlength[findlength.IndexOf(num) + num.Length] == '.')
    //                    increaseByHalf = true;
    //                else
    //                    increaseByHalf = false;
    //                i += num.Length;
    //                if (increaseByHalf)
    //                    i++;
    //                Debug.WriteLine("Length set to: " + currLength);
    //            }
    //            //Octaves
    //            else if (currC == 'O' && char.IsDigit(c[i + 1]))
    //            {
    //                string num = new string(c.Skip(i + 1).TakeWhile(char.IsDigit).ToArray());
    //                currOctave = (byte)(byte.Parse(num)-1);
    //                i += num.Length;
    //                Debug.WriteLine("Octave set to: " + currOctave);
    //            }
    //            else if (currC == '>')
    //            {
    //                currOctave++;
    //            }
    //            else if (currC == '<')
    //            {
    //                currOctave--;
    //            }
    //            //Rest
    //            else if (currC == 'R')
    //            {
    //                string num = new string(c.Skip(i + 1).TakeWhile(char.IsDigit).ToArray());
    //                byte l = string.IsNullOrWhiteSpace(num) ? currLength : byte.Parse(num);
    //                var r = new MMLRest { Length = 60000 / currTempo * (4 / (float)l) };
    //                i += num.Length;
    //                if (c[i] == '.')
    //                {
    //                    r.Length *= (float)1.5;
    //                    i++;
    //                }
    //                Sheet.Add(r);
    //            }
    //            //Notes
    //            else if (basicnotes.Contains(char.ToUpperInvariant(currC)))
    //            {
    //                char n = char.ToUpperInvariant(currC);
    //                MMLNote note = new MMLNote
    //                {
    //                    Id = (byte)(currOctave * 12)
    //                };
    //                if (c[i + 1] == '+' || c[i + 1] == '-')
    //                {
    //                    note.Id += EnharmonicEquivalent(n.ToString() + c[i + 1].ToString());
    //                    i++;
    //                }
    //                else
    //                {
    //                    note.Id += notes.Single(x => x.Value == n.ToString()).Key;
    //                }
    //                if (char.IsDigit(c[i + 1]))
    //                {
    //                    string num = new string(c.Skip(i + 1).TakeWhile(char.IsDigit).ToArray());
    //                    byte l = string.IsNullOrWhiteSpace(num) ? currLength : byte.Parse(num);
    //                    note.Length = 60000 / currTempo * (4 / (float)l);
    //                    i++;
    //                }
    //                else
    //                    note.Length = 60000 / currTempo * (4 / (float)currLength);
    //                if (c[i + 1] == '.' || increaseByHalf)
    //                {
    //                    note.Length *= (float)1.5;
    //                    i++;
    //                }
    //                Sheet.Add(note);
    //                Debug.WriteLine("Added " + note.Id);
    //            }
    //            //Midi note
    //            else if(currC == 'N' && char.IsDigit(c[i + 1]))
    //            {
    //                var n = new MMLNote
    //                {
    //                    Id = byte.Parse(new string(c.Skip(i + 1).TakeWhile(char.IsDigit).ToArray())),
    //                    Length = 60000 / currTempo * (4 / (float)currLength)
    //                };
    //                Sheet.Add(n);
    //                i += n.Id.ToString().Length + 1;
    //            }
    //        }
    //    }

    //    private static byte EnharmonicEquivalent(string note)
    //    {
    //        if (notes.Any(x => x.Value == note.Replace('+', '#')))
    //            return notes.Single(x => x.Value == note.Replace('+', '#')).Key;
    //        string nn = null;
    //        if (note[1] == '-')
    //        {
    //            nn = note.Replace('-', '#');
    //            nn = (note[0] == 'A') ? nn.ReplaceAt(0, 'G') : nn.ReplaceAt(0, nn[0].Decrement());
    //        }
    //        //else if (note[1] == '+')
    //        //{
    //        //    nn = note.Replace('+', '#');
    //        //    nn = (note[0] == 'G') ? nn.ReplaceAt(0, 'A') : nn.ReplaceAt(0, nn[0].Increment());
    //        //}
    //        if (notes.Any(x => x.Value == nn))
    //            return notes.Single(x => x.Value == nn).Key;
    //        else
    //        {
    //            nn = note.Remove(1, 1);
    //            nn = (note[0] == 'G') ? nn.ReplaceAt(0, 'A') : nn.ReplaceAt(0, nn[0].Increment());
    //            if (notes.Any(x => x.Value == nn))
    //                return nn=="C" ? (byte)(notes.Single(x => x.Value == nn).Key + 12) : notes.Single(x => x.Value == nn).Key;
    //        }
    //        return 0;
    //    }

    //    internal async Task PlayAsync(int id, CancellationToken cts)
    //    {
    //        foreach (MMLCommand c in Sheet)
    //        {
    //            switch(c)
    //            {
    //                case MMLNote n: PersistentNamedPipeServer.SendPipeMessage(new PipeMessage { PID = id, Cmd = PMCommand.PlayNote, Parameter = n.Id });
    //                    await Task.Delay((int)n.Length);
    //                    break;
    //                case MMLRest n: await Task.Delay((int)n.Length);
    //                    break;
    //            }                
    //            if (cts.IsCancellationRequested)
    //                break;
    //        }
    //    }
    //}

    //abstract class MMLCommand { }

    //class MMLNote : MMLCommand
    //{
    //    public byte Id { get; set; }
    //    public float Length { get; set; }
    //}

    //class MMLRest : MMLCommand
    //{
    //    public float Length { get; set; }
    //}
}