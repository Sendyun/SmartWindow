using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SmartWindow
{
    internal sealed class LegacySponsorForm : Form
    {
        private const string AfdianUrl =
            "https://afdian.com/a/sendyun";
        private const string WebsiteUrl =
            "https://cnsendyun.com";
        private readonly List<Bitmap> sponsorImages =
            new List<Bitmap>();

        public LegacySponsorForm()
        {
            Text = Localization.Get("sponsorWindowTitle");
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(13, 15, 19);
            ForeColor = Color.FromArgb(238, 241, 240);
            Font = new Font(
                Localization.PreferredFontFamily,
                9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(780, 660);

            Label title = new Label();
            title.SetBounds(28, 20, 720, 36);
            title.Font = new Font(
                Localization.PreferredFontFamily,
                18f,
                FontStyle.Bold);
            title.ForeColor = Color.FromArgb(248, 244, 232);
            title.Text = Localization.Get("sponsorTitle");
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.SetBounds(30, 59, 700, 24);
            subtitle.ForeColor = Color.FromArgb(185, 194, 197, 199);
            subtitle.Text = Localization.Get("sponsorSubtitle");
            Controls.Add(subtitle);

            Panel intro = new Panel();
            intro.SetBounds(28, 94, 724, 154);
            intro.BackColor = Color.FromArgb(22, 25, 30);
            Controls.Add(intro);
            Label body = new Label();
            body.SetBounds(18, 13, 688, 126);
            body.ForeColor = Color.FromArgb(220, 231, 233, 231);
            body.Font = new Font(
                Localization.PreferredFontFamily,
                9.6f);
            body.Text =
                Localization.Get("sponsorBody") +
                Environment.NewLine +
                Environment.NewLine +
                Localization.Get("sponsorMinimum");
            intro.Controls.Add(body);

            TabControl tabs = new TabControl();
            tabs.SetBounds(28, 262, 724, 316);
            tabs.Padding = new Point(14, 7);
            Controls.Add(tabs);

            TabPage afdianPage = new TabPage(
                Localization.Get("afdianSupport"));
            afdianPage.BackColor = Color.FromArgb(17, 19, 24);
            afdianPage.ForeColor = ForeColor;
            tabs.TabPages.Add(afdianPage);

            Label afdianDescription = new Label();
            afdianDescription.SetBounds(22, 25, 650, 70);
            afdianDescription.ForeColor =
                Color.FromArgb(195, 210, 214, 214);
            afdianDescription.Text =
                Localization.Get("sponsorAfdianHint");
            afdianPage.Controls.Add(afdianDescription);

            Button afdianButton = CreateButton(
                Localization.Get("openAfdian"),
                22,
                112,
                170);
            afdianButton.BackColor = Color.FromArgb(130, 102, 58);
            afdianButton.FlatAppearance.BorderColor =
                Color.FromArgb(188, 153, 91);
            afdianButton.Click += delegate
            {
                OpenUrl(AfdianUrl);
            };
            afdianPage.Controls.Add(afdianButton);

            Button copyButton = CreateButton(
                Localization.Get("copyAfdianLink"),
                204,
                112,
                170);
            copyButton.Click += delegate
            {
                try
                {
                    Clipboard.SetText(AfdianUrl);
                }
                catch (Exception exception)
                {
                    Logger.Write("Copy donation link: " + exception);
                }
            };
            afdianPage.Controls.Add(copyButton);

            Button websiteButton = CreateButton(
                Localization.Get("visitOfficialSite"),
                386,
                112,
                170);
            websiteButton.Click += delegate
            {
                OpenUrl(WebsiteUrl);
            };
            afdianPage.Controls.Add(websiteButton);

            Label legal = new Label();
            legal.SetBounds(22, 188, 650, 62);
            legal.ForeColor =
                Color.FromArgb(142, 190, 194, 194);
            legal.Text = Localization.Get("sponsorLegal");
            afdianPage.Controls.Add(legal);

            Bitmap alipayImage =
                RuntimeAssets.LoadDonationAlipay();
            Bitmap wechatImage =
                RuntimeAssets.LoadDonationWechat();
            if (alipayImage != null)
            {
                sponsorImages.Add(alipayImage);
            }
            if (wechatImage != null)
            {
                sponsorImages.Add(wechatImage);
            }
            AddQrTab(
                tabs,
                Localization.Get("alipaySupport"),
                alipayImage);
            AddQrTab(
                tabs,
                Localization.Get("wechatSupport"),
                wechatImage);

            Button close = CreateButton(
                Localization.Get("close"),
                616,
                595,
                136);
            close.Click += delegate
            {
                Close();
            };
            Controls.Add(close);
            CancelButton = close;
            FormClosed += delegate
            {
                for (int index = 0;
                    index < sponsorImages.Count;
                    index++)
                {
                    sponsorImages[index].Dispose();
                }
                sponsorImages.Clear();
            };
        }

        private static void AddQrTab(
            TabControl tabs,
            string title,
            Bitmap image)
        {
            TabPage page = new TabPage(title);
            page.BackColor = Color.FromArgb(17, 19, 24);
            page.ForeColor = Color.FromArgb(236, 240, 239);
            tabs.TabPages.Add(page);
            PictureBox picture = new PictureBox();
            picture.Dock = DockStyle.Fill;
            picture.Margin = new Padding(12);
            picture.Padding = new Padding(12);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            picture.BackColor = Color.FromArgb(24, 26, 31);
            picture.Image = image;
            page.Controls.Add(picture);
        }

        private static Button CreateButton(
            string text,
            int left,
            int top,
            int width)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(left, top, width, 38);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor =
                Color.FromArgb(62, 255, 255, 255);
            button.BackColor = Color.FromArgb(39, 42, 49);
            button.ForeColor = Color.FromArgb(238, 241, 240);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                Logger.Write("Open sponsor URL: " + exception);
            }
        }
    }

    internal sealed class NotificationManagementForm : Form
    {
        private readonly ListView historyList;
        private readonly CheckedListBox filterList;
        private readonly TextBox appInput;
        private readonly TabControl tabs;

        public bool HistoryCleared;
        public HashSet<string> ResultFilters;

        public NotificationManagementForm(
            IList<NotificationHistoryEntry> history,
            IEnumerable<string> currentFilters,
            bool showHistory)
        {
            Text = Localization.Get("notificationManagement");
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(12, 14, 18);
            ForeColor = Color.FromArgb(235, 240, 242);
            Font = new Font(
                Localization.PreferredFontFamily,
                9.5f,
                FontStyle.Regular);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(780, 520);

            HashSet<string> filters = new HashSet<string>(
                currentFilters ?? new string[0],
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> knownApps = new HashSet<string>(
                filters,
                StringComparer.OrdinalIgnoreCase);

            tabs = new TabControl();
            tabs.Dock = DockStyle.Top;
            tabs.Height = 420;
            tabs.Padding = new Point(14, 7);
            Controls.Add(tabs);

            TabPage historyPage = new TabPage(
                Localization.Get("notificationHistory"));
            historyPage.BackColor = Color.FromArgb(16, 18, 23);
            historyPage.ForeColor = ForeColor;
            tabs.TabPages.Add(historyPage);

            historyList = new ListView();
            historyList.Dock = DockStyle.Fill;
            historyList.View = View.Details;
            historyList.FullRowSelect = true;
            historyList.GridLines = false;
            historyList.HideSelection = false;
            historyList.BackColor = Color.FromArgb(22, 24, 29);
            historyList.ForeColor = Color.FromArgb(235, 239, 240);
            historyList.BorderStyle = BorderStyle.None;
            historyList.Columns.Add(
                Localization.Get("historyTime"),
                115);
            historyList.Columns.Add(
                Localization.Get("historyApp"),
                130);
            historyList.Columns.Add(
                Localization.Get("historyTitle"),
                180);
            historyList.Columns.Add(
                Localization.Get("historyContent"),
                320);
            if (history != null)
            {
                for (int index = 0; index < history.Count; index++)
                {
                    NotificationHistoryEntry item = history[index];
                    ListViewItem row = new ListViewItem(
                        item.ReceivedAt.ToString("MM-dd HH:mm:ss"));
                    row.SubItems.Add(item.AppName ?? "");
                    row.SubItems.Add(item.Title ?? "");
                    row.SubItems.Add(item.Body ?? "");
                    if (item.WasFiltered || item.WasSuppressed)
                    {
                        row.ForeColor = Color.FromArgb(150, 170, 175, 180);
                    }
                    historyList.Items.Add(row);
                    if (!string.IsNullOrWhiteSpace(item.AppName))
                    {
                        knownApps.Add(item.AppName.Trim());
                    }
                }
            }
            historyPage.Controls.Add(historyList);

            Panel historyActions = new Panel();
            historyActions.Dock = DockStyle.Bottom;
            historyActions.Height = 48;
            historyActions.BackColor = Color.FromArgb(16, 18, 23);
            Button clearButton = CreateButton(
                Localization.Get("clearHistory"),
                12);
            clearButton.Click += delegate
            {
                HistoryCleared = true;
                historyList.Items.Clear();
            };
            historyActions.Controls.Add(clearButton);
            historyPage.Controls.Add(historyActions);

            TabPage filterPage = new TabPage(
                Localization.Get("appFilters"));
            filterPage.BackColor = Color.FromArgb(16, 18, 23);
            filterPage.ForeColor = ForeColor;
            tabs.TabPages.Add(filterPage);

            filterList = new CheckedListBox();
            filterList.Dock = DockStyle.Fill;
            filterList.CheckOnClick = true;
            filterList.BackColor = Color.FromArgb(22, 24, 29);
            filterList.ForeColor = Color.FromArgb(235, 239, 240);
            filterList.BorderStyle = BorderStyle.None;
            filterList.Font = new Font(
                Localization.PreferredFontFamily,
                10.2f);
            foreach (string appName in knownApps)
            {
                filterList.Items.Add(
                    appName,
                    filters.Contains(appName));
            }
            filterPage.Controls.Add(filterList);

            Panel filterInputPanel = new Panel();
            filterInputPanel.Dock = DockStyle.Bottom;
            filterInputPanel.Height = 76;
            filterInputPanel.BackColor = Color.FromArgb(16, 18, 23);
            Label filterHint = new Label();
            filterHint.AutoSize = false;
            filterHint.SetBounds(12, 4, 720, 22);
            filterHint.ForeColor = Color.FromArgb(150, 210, 214, 216);
            filterHint.Text = Localization.Get("filterHint");
            filterInputPanel.Controls.Add(filterHint);
            appInput = new TextBox();
            appInput.SetBounds(12, 31, 520, 30);
            appInput.BackColor = Color.FromArgb(27, 30, 36);
            appInput.ForeColor = ForeColor;
            appInput.BorderStyle = BorderStyle.FixedSingle;
            appInput.Font = new Font(
                Localization.PreferredFontFamily,
                10f);
            filterInputPanel.Controls.Add(appInput);
            Button addButton = CreateButton(
                Localization.Get("addFilterApp"),
                550);
            addButton.Width = 110;
            addButton.SetBounds(550, 30, 110, 32);
            addButton.Click += delegate
            {
                string value = appInput.Text.Trim();
                if (value.Length == 0)
                {
                    return;
                }
                for (int index = 0;
                    index < filterList.Items.Count;
                    index++)
                {
                    if (string.Equals(
                        Convert.ToString(filterList.Items[index]),
                        value,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        filterList.SetItemChecked(index, true);
                        appInput.Clear();
                        return;
                    }
                }
                int added = filterList.Items.Add(value, true);
                filterList.SelectedIndex = added;
                appInput.Clear();
            };
            filterInputPanel.Controls.Add(addButton);
            filterPage.Controls.Add(filterInputPanel);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 62;
            bottom.BackColor = Color.FromArgb(12, 14, 18);
            Button saveButton = CreateButton(
                Localization.Get("save"),
                474);
            saveButton.SetBounds(474, 13, 132, 36);
            saveButton.BackColor = Color.FromArgb(90, 142, 118);
            saveButton.Click += delegate
            {
                SaveFilters(filters);
                DialogResult = DialogResult.OK;
                Close();
            };
            bottom.Controls.Add(saveButton);
            Button closeButton = CreateButton(
                Localization.Get("close"),
                616);
            closeButton.SetBounds(616, 13, 132, 36);
            closeButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            bottom.Controls.Add(closeButton);
            Controls.Add(bottom);

            tabs.SelectedIndex = showHistory ? 0 : 1;
            AcceptButton = saveButton;
            CancelButton = closeButton;
        }

        private void SaveFilters(HashSet<string> previous)
        {
            HashSet<string> selected = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0;
                index < filterList.Items.Count;
                index++)
            {
                if (filterList.GetItemChecked(index))
                {
                    string value = Convert.ToString(
                        filterList.Items[index]).Trim();
                    if (value.Length > 0)
                    {
                        selected.Add(value);
                    }
                }
            }
            ResultFilters = selected;
        }

        private static Button CreateButton(string text, int left)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(left, 8, 132, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor =
                Color.FromArgb(55, 255, 255, 255);
            button.BackColor = Color.FromArgb(39, 42, 49);
            button.ForeColor = Color.FromArgb(235, 240, 242);
            button.Cursor = Cursors.Hand;
            return button;
        }
    }
}
