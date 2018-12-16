using GTA;
using GTA.Native;
using Newtonsoft.Json;
using SimpleRadio.Items;
using SimpleRadio.Streaming;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SimpleRadio
{
    public class SimpleRadio : Script
    {
        private StreamPlayer Streaming = new StreamPlayer();
        private List<Radio> Radios = new List<Radio>();
        private Radio Selected = null;

        /// <summary>
        /// The previous radio.
        /// </summary>
        public Radio Previous
        {
            get
            {
                int CurrentIndex = Radios.IndexOf(Selected);
                return Radios[CurrentIndex == 0 ? Radios.Count - 1 : CurrentIndex - 1];
            }
        }
        /// <summary>
        /// The next radio.
        /// </summary>
        public Radio Next
        {
            get
            {
                int CurrentIndex = Radios.IndexOf(Selected);
                return Radios[CurrentIndex == Radios.Count - 1 ? 0 : CurrentIndex + 1];
            }
        }

        public SimpleRadio()
        {
            // Create an item for turning the radio off
            Radio Off = new Radio()
            {
                Name = "Radio Off",
                Frequency = 0,
                Type = RadioType.Vanilla,
                ID = 255
            };
            // Add this item
            Radios.Add(Off);
            // Open the JSON file for reading
            using (StreamReader Reader = new StreamReader("scripts\\SimpleRadio\\Vanilla.json"))
            {
                // Read the file content
                string JSON = Reader.ReadToEnd();
                // And add it onto the existing list of radios
                Radios.AddRange(JsonConvert.DeserializeObject<ConfigFile>(JSON).Radios);
            }

            // Set the selected radio as off, just in case
            Selected = Off;

            // Order the radios by frequency
            Radios.OrderBy(X => X.Frequency);

            // And add our events
            Tick += OnTickFixes;
            Tick += OnTickControls;
            Tick += OnTickDraw;
            Aborted += (Sender, Args) => { Streaming.Stop(); };

            // Enable the mobile phone radio
            Function.Call(Hash.SET_MOBILE_PHONE_RADIO_STATE, true);
        }

        private void OnTickFixes(object Sender, EventArgs Args)
        {
            // Enable the mobile radio this tick
            Function.Call(Hash.SET_MOBILE_RADIO_ENABLED_DURING_GAMEPLAY, true);

            // If the selected radio type is vanilla and the current station does not matches
            if (Selected.Type == RadioType.Vanilla || Selected.ID != (int)Game.RadioStation)
            {
                // Get the item where the radio ID matches
                Radio TempRadio = Radios.SingleOrDefault(X => X.ID == (int)Game.RadioStation);
                // If is not null, use it
                if (TempRadio != null)
                {
                    Selected = TempRadio;
                }
                // If if null, set the radio as off
                else
                {
                    Selected = Radios.SingleOrDefault(X => X.Name == "Radio Off");
                }
            }
        }

        private void OnTickControls(object Sender, EventArgs Args)
        {
            // Disable the weapon wheel
            Game.DisableControlThisFrame(0, Control.VehicleRadioWheel);
            Game.DisableControlThisFrame(0, Control.VehicleNextRadio);
            Game.DisableControlThisFrame(0, Control.VehiclePrevRadio);

            // Check if a control has been pressed
            if (Game.IsDisabledControlJustPressed(0, Control.VehicleRadioWheel) || Game.IsDisabledControlJustPressed(0, Control.VehicleNextRadio))
            {
                NextRadio();
            }
        }

        private void OnTickDraw(object Sender, EventArgs Args)
        {
            // If there is a frequency, add it at the end like every normal radio ad
            string RadioName = Selected.Frequency == 0 ? Selected.Name : Selected.Name + " " + Selected.Frequency.ToString();

            // Draw the previous, current and next radio name
            UIText PreviousUI = new UIText(Previous.Name, new Point((int)(UI.WIDTH * .5f), (int)(UI.HEIGHT * .025f)), .5f, Color.LightGray, GTA.Font.ChaletLondon, true, true, false);
            PreviousUI.Draw();
            UIText CurrentUI = new UIText(RadioName, new Point((int)(UI.WIDTH * .5f), (int)(UI.HEIGHT * .055f)), .6f, Color.White, GTA.Font.ChaletLondon, true, true, false);
            CurrentUI.Draw();
            UIText NextUI = new UIText(Next.Name, new Point((int)(UI.WIDTH * .5f), (int)(UI.HEIGHT * .09f)), .5f, Color.LightGray, GTA.Font.ChaletLondon, true, true, false);
            NextUI.Draw();
        }

        private void NextRadio()
        {
            // Is the next radio is vanilla
            if (Next.Type == RadioType.Vanilla)
            {
                Game.RadioStation = (RadioStation)Next.ID;
            }
        }
    }
}
