using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CrayonTodo
{
    internal sealed class ThemePalette
    {
        public Color Ink, Background, Accent, Soft, Timer, Card, Border, Secondary;
        public string Name, Resource, Phrase;
        private static Color C(string hex) { return ColorTranslator.FromHtml(hex); }
        public static ThemePalette For(string key)
        {
            switch (key)
            {
                case "kitty": return new ThemePalette { Name="Hello Kitty", Resource="CrayonTodo.Kitty", Phrase="把今天过成喜欢的样子", Background=C("#fffbfc"), Ink=C("#4d3a40"), Accent=C("#e96e91"), Soft=C("#ffe4ec"), Timer=C("#fff0f4"), Card=C("#fff7f9"), Border=C("#f1dfe4"), Secondary=C("#a1818d") };
                case "kuromi": return new ThemePalette { Name="库洛米", Resource="CrayonTodo.Kuromi", Phrase="今天也要闪闪发光", Background=C("#fcfaff"), Ink=C("#3b314a"), Accent=C("#8b6ab1"), Soft=C("#ede5fa"), Timer=C("#f1ebfa"), Card=C("#f9f5ff"), Border=C("#e5daf2"), Secondary=C("#9283a4") };
                case "spongebob": return new ThemePalette { Name="海绵宝宝", Resource="CrayonTodo.SpongeBob", Phrase="快乐出发，一起加油", Background=C("#fffdf3"), Ink=C("#4b4330"), Accent=C("#e8b630"), Soft=C("#fff0bf"), Timer=C("#fff5cf"), Card=C("#fffaf0"), Border=C("#eee4bc"), Secondary=C("#9d8f6a") };
                case "doraemon": return new ThemePalette { Name="哆啦 A 梦", Resource="CrayonTodo.Doraemon", Phrase="把小目标装进口袋", Background=C("#f9fdff"), Ink=C("#304453"), Accent=C("#45a5d4"), Soft=C("#dff3fc"), Timer=C("#e8f7ff"), Card=C("#f3fbff"), Border=C("#d4e9f4"), Secondary=C("#7e9eaf") };
                default: return new ThemePalette { Name="蜡笔小新", Resource="CrayonTodo.Shinchan", Phrase="今天的事情，一件一件来。", Background=C("#fff9e2"), Ink=C("#42312a"), Accent=C("#f86953"), Soft=C("#ffda5d"), Timer=C("#ffe8bb"), Card=C("#ffe5e1"), Border=C("#42312a"), Secondary=C("#835d4c") };
            }
        }
        public static Image LoadImage(string resource)
        {
            using (Stream stream = typeof(ThemePalette).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) return null;
                using (Image image = Image.FromStream(stream)) return new Bitmap(image);
            }
        }
    }
}
