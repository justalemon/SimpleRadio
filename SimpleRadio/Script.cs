using GTA;
using SimpleRadio.Streaming;
using System.Windows.Forms;

namespace SimpleRadio
{
    public class SimpleRadio : Script
    {
        private StreamPlayer Streaming = new StreamPlayer();

        public SimpleRadio()
        {
            KeyDown += Test;
            Aborted += (Sender, Args) => { Streaming.Stop(); };
        }

        private void Test(object Sender, KeyEventArgs E)
        {
            if (E.KeyCode == Keys.PageUp)
            {
                Streaming.Play("http://unlimited11-cl.dps.live/candela/mp3/icecast.audio");
            }
            else if (E.KeyCode == Keys.PageDown)
            {
                Streaming.Stop();
            }
        }
    }
}
