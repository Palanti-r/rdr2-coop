using System;
using System.Collections.Generic;
using System.Drawing;
using RDR2Coop.Net;

namespace RDR2Coop.Mod
{
    static class EmoteWheelView
    {
        const int OuterRadius = 164, InnerRadius = 66;
        const float CenterX = 640f, CenterY = 342f, IconRadius = 115f, IconSize = 47f;
        const int Outside = -2, Hub = -1;
        const string IconDictionary = "multiwheel_emotes";
        static readonly Color Segment = Color.FromArgb(115, 22, 23, 26);
        static readonly Color Center = Color.FromArgb(145, 16, 17, 20);
        static readonly Color Highlight = Color.FromArgb(153, 191, 113, 47);
        static readonly Color Ivory = Color.FromArgb(255, 250, 244, 231);
        static readonly Color Muted = Color.FromArgb(180, 250, 244, 231);
        static readonly Color Accent = Color.FromArgb(255, 255, 217, 155);

        // The game's streamed HUD icons, in EmoteList order.
        static readonly string[] Icons =
        {
            "emote_greet_gentlewave", "emote_greet_tip_hat", "emote_action_point", "emote_greet_get_over_here",
            "emote_reaction_applause", "emote_reaction_jovial_laugh", "emote_reaction_nod_head", "emote_reaction_shake_head"
        };

        static readonly double SectorAngle = 2.0 * Math.PI / EmoteList.All.Length;
        static readonly (RectangleF Bounds, int Sector)[] Backdrop = BuildBackdrop();

        public static void Draw(int selected, Func<int, bool> unavailable, string controls)
        {
            Ui.Centered(true, preserveAspect: true);
            try
            {
                foreach (var (bounds, sector) in Backdrop)
                {
                    Color color = sector == Hub ? Center : sector == selected ? Highlight : Segment;
                    Ui.Box(CenterX + bounds.X, CenterY + bounds.Y, bounds.Width, bounds.Height, color);
                }

                var emotes = EmoteList.All;
                for (int i = 0; i < emotes.Length; i++)
                {
                    double angle = i * SectorAngle;
                    float x = CenterX + IconRadius * (float)Math.Sin(angle);
                    float y = CenterY - IconRadius * (float)Math.Cos(angle);
                    Color color = i == selected ? Accent : unavailable(i) ? Muted : Ivory;
                    if (i >= Icons.Length || !Ui.Sprite(IconDictionary, Icons[i], x - IconSize / 2f, y - IconSize / 2f, IconSize, IconSize, color))
                        TextCentered(emotes[i].Label, x, y - 9f, 0.29f, color);
                }

                string label = selected < 0 ? "Choose an emote" : emotes[selected].Label;
                TextCentered(label, CenterX, CenterY - 12f, 0.37f, selected < 0 ? Ivory : Accent);
                if (selected >= 0 && unavailable(selected))
                    TextCentered("Unavailable", CenterX, CenterY + 8f, 0.27f, Muted);
                TextCentered(controls, CenterX, CenterY + OuterRadius + 12f, 0.30f, Ivory);
            }
            finally { Ui.Centered(false); }
        }

        static void TextCentered(string text, float x, float y, float scale, Color color)
        {
            // The text native takes a left edge. Estimate the width for these short, fixed HUD labels.
            Ui.TextAt(text, x - text.Length * 9f * scale, y, scale, color, Ui.BodyFont);
        }

        // Cache horizontal spans for the HUD's rectangle native. Each pixel belongs to at most
        // one span so the highlighted segment and center retain their intended transparency.
        static (RectangleF, int)[] BuildBackdrop()
        {
            const int bandHeight = 4, sampleWidth = 2;
            var spans = new List<(RectangleF, int)>();
            for (int y = -OuterRadius; y < OuterRadius; y += bandHeight)
            {
                int start = -OuterRadius, previous = Outside;
                float sampleY = y + bandHeight / 2f;
                for (int x = -OuterRadius; x <= OuterRadius; x += sampleWidth)
                {
                    int sector = SectorAt(x + sampleWidth / 2f, sampleY);
                    if (sector == previous) continue;
                    if (previous != Outside)
                        spans.Add((new RectangleF(start, y, x - start, bandHeight), previous));
                    previous = sector;
                    start = x;
                }
            }
            return spans.ToArray();
        }

        static int SectorAt(float x, float y)
        {
            float squaredDistance = x * x + y * y;
            if (squaredDistance >= OuterRadius * OuterRadius) return Outside;
            if (squaredDistance <= InnerRadius * InnerRadius) return Hub;

            double angle = Math.Atan2(x, -y) / SectorAngle;
            int slot = (int)Math.Floor(angle + 0.5);
            double distanceToDivider = Math.Sin((0.5 - Math.Abs(angle - slot)) * SectorAngle) * Math.Sqrt(squaredDistance);
            return distanceToDivider < 2.25 ? Outside : (slot + EmoteList.All.Length) % EmoteList.All.Length;
        }
    }
}
