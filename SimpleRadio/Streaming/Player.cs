using GTA;
using NAudio.Wave;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Timers;

namespace SimpleRadio.Streaming
{
    /// <summary>
    /// Class used for handling the stream connections.
    /// </summary>
    public class StreamPlayer
    {
        private System.Timers.Timer HandlingTimer = new System.Timers.Timer();

        public StreamPlayer()
        {
            HandlingTimer.Interval = 250;
            HandlingTimer.Elapsed += new ElapsedEventHandler(timer1_Tick);
            //Disposed += MP3StreamingPanel_Disposing;
            //volumeSlider1.VolumeChanged += OnVolumeSliderChanged;
        }

        private BufferedWaveProvider bufferedWaveProvider;
        private IWavePlayer waveOut;
        private volatile StreamingState playbackState;
        private volatile bool fullyDownloaded;
        private HttpWebRequest webRequest;
        private VolumeWaveProvider16 volumeProvider;

        delegate void ShowErrorDelegate(string message);

        private void ShowError(string message)
        {
            //UI.Notify(message);
        }

        private void StreamMp3(object state)
        {
            fullyDownloaded = false;
            var url = (string)state;
            webRequest = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)webRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled)
                {
                    ShowError(e.Message);
                }
                return;
            }
            var buffer = new byte[16384 * 4]; // needs to be big enough to hold a decompressed frame

            IMp3FrameDecompressor decompressor = null;
            try
            {
                using (var responseStream = resp.GetResponseStream())
                {
                    var readFullyStream = new FullStream(responseStream);
                    do
                    {
                        if (IsBufferNearlyFull)
                        {
                            //UI.Notify("Buffer getting full, taking a break");
                            Thread.Sleep(500);
                        }
                        else
                        {
                            Mp3Frame frame;
                            try
                            {
                                frame = Mp3Frame.LoadFromStream(readFullyStream);
                            }
                            catch (EndOfStreamException)
                            {
                                fullyDownloaded = true;
                                // reached the end of the MP3 file / stream
                                break;
                            }
                            catch (WebException)
                            {
                                // probably we have aborted download from the GUI thread
                                break;
                            }
                            if (frame == null) break;
                            if (decompressor == null)
                            {
                                // don't think these details matter too much - just help ACM select the right codec
                                // however, the buffered provider doesn't know what sample rate it is working at
                                // until we have a frame
                                decompressor = CreateFrameDecompressor(frame);
                                bufferedWaveProvider = new BufferedWaveProvider(decompressor.OutputFormat);
                                bufferedWaveProvider.BufferDuration =
                                    TimeSpan.FromSeconds(20); // allow us to get well ahead of ourselves
                                //this.bufferedWaveProvider.BufferedDuration = 250;
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            //Debug.WriteLine(String.Format("Decompressed a frame {0}", decompressed));
                            bufferedWaveProvider.AddSamples(buffer, 0, decompressed);
                        }

                    } while (playbackState != StreamingState.Stopped);
                    //UI.Notify("Exiting");
                    // was doing this in a finally block, but for some reason
                    // we are hanging on response stream .Dispose so never get there
                    decompressor.Dispose();
                }
            }
            finally
            {
                if (decompressor != null)
                {
                    decompressor.Dispose();
                }
            }
        }

        private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
        {
            WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            return new AcmMp3FrameDecompressor(waveFormat);
        }

        private bool IsBufferNearlyFull
        {
            get
            {
                return bufferedWaveProvider != null &&
                       bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
                       < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
            }
        }

        public void Stop()
        {
            if (playbackState != StreamingState.Stopped)
            {
                if (!fullyDownloaded)
                {
                    webRequest.Abort();
                }

                playbackState = StreamingState.Stopped;
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                }
                HandlingTimer.Enabled = false;
                // n.b. streaming thread may not yet have exited
                //Thread.Sleep(500);
                ShowBufferState(0);
            }
        }

        private void ShowBufferState(double totalSeconds)
        {
            //labelBuffered.Text = String.Format("{0:0.0}s", totalSeconds);
            //progressBarBuffer.Value = (int)(totalSeconds * 1000);
        }

        private void timer1_Tick(object sender, ElapsedEventArgs e)
        {
            if (playbackState != StreamingState.Stopped)
            {
                if (waveOut == null && bufferedWaveProvider != null)
                {
                    //UI.Notify("Creating WaveOut Device");
                    waveOut = CreateWaveOut();
                    waveOut.PlaybackStopped += OnPlaybackStopped;
                    volumeProvider = new VolumeWaveProvider16(bufferedWaveProvider);
                    //volumeProvider.Volume = volumeSlider1.Volume;
                    waveOut.Init(volumeProvider);
                    //progressBarBuffer.Maximum = (int)bufferedWaveProvider.BufferDuration.TotalMilliseconds;
                }
                else if (bufferedWaveProvider != null)
                {
                    var bufferedSeconds = bufferedWaveProvider.BufferedDuration.TotalSeconds;
                    ShowBufferState(bufferedSeconds);
                    // make it stutter less if we buffer up a decent amount before playing
                    if (bufferedSeconds < 0.5 && playbackState == StreamingState.Playing && !fullyDownloaded)
                    {
                        Pause();
                    }
                    else if (bufferedSeconds > 4 && playbackState == StreamingState.Buffering)
                    {
                        Play();
                    }
                    else if (fullyDownloaded && bufferedSeconds == 0)
                    {
                        //UI.Notify("Reached end of stream");
                        Stop();
                    }
                }

            }
        }

        public void Play()
        {
            waveOut.Play();
            //UI.Notify(String.Format("Started playing, waveOut.PlaybackState={0}", waveOut.PlaybackState));
            playbackState = StreamingState.Playing;
        }

        public void Play(string URL)
        {
            Stop();
            playbackState = StreamingState.Buffering;
            bufferedWaveProvider = null;
            ThreadPool.QueueUserWorkItem(StreamMp3, URL);
            HandlingTimer.Enabled = true;
        }

        private void Pause()
        {
            playbackState = StreamingState.Buffering;
            waveOut.Pause();
            //UI.Notify(String.Format("Paused to buffer, waveOut.PlaybackState={0}", waveOut.PlaybackState));
        }

        private IWavePlayer CreateWaveOut()
        {
            return new WaveOut();
        }

        private void buttonPause_Click(object sender, EventArgs e)
        {
            if (playbackState == StreamingState.Playing || playbackState == StreamingState.Buffering)
            {
                waveOut.Pause();
                //UI.Notify(String.Format("User requested Pause, waveOut.PlaybackState={0}", waveOut.PlaybackState));
                playbackState = StreamingState.Paused;
            }
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            //UI.Notify("Playback Stopped");
            if (e.Exception != null)
            {
                //UI.Notify(String.Format("Playback Error {0}", e.Exception.Message));
            }
        }
    }
}
