using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ReactionTrainer;

public partial class Form1 : Form
{
    private Stopwatch _stopwatch = new Stopwatch();
    private Random _random = new Random();
    private System.Windows.Forms.Timer _gameLoop = null!;

    // FPS Tracking
    private int _fps = 0;
    private int _frameCount = 0;
    private Stopwatch _fpsStopwatch = new Stopwatch();

    private class Target {
        public PointF Pos;
        public float VX, VY, TVX, TVY;
        public float Radius = 20; 
    }
    private List<Target> _targets = new();

    private int _points = 0, _roundHits = 0, _roundMisses = 0;
    private int _highestHits = 0; 
    private int _streak = 0, _bestStreak = 0;
    private int _missCount = 0; 
    private long _bestScore = long.MaxValue;
    private string _savePath;
    
    private bool _showMarks = true;

    private static readonly byte[] Key = Encoding.UTF8.GetBytes("R3act!onTr4in3r_S3cur1ty_K3y_256"); 
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456"); 

    private enum MarkerType { HitX, BulletHole }
    private class WallMarker { public Point Location; public MarkerType Type; }
    private List<WallMarker> _wallMarkers = new();
    
    private class FloatingText { public string Text = ""; public PointF Location; public Color Color; public float Opacity = 1.0f; }
    private List<FloatingText> _floatingTexts = new();
    
    private Label _fpsLabel = null!, _highScoreLabel = null!, _pointsLabel = null!, _hitsLabel = null!, _missesLabel = null!;
    private bool _isPaused = false;
    private Panel _menuPanel = null!, _settingsPanel = null!, _creditsPanel = null!;

    public Form1()
    {
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.DoubleBuffered = true;
        this.KeyPreview = true;
        this.StartPosition = FormStartPosition.CenterScreen;

        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReactionTrainer");
        if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
        _savePath = Path.Combine(appData, "game.dat");

        LoadGameData();
        SetupGame();
        SetupMenu();
    }

    private void SetupGame()
    {
        this.Text = "Reaction Trainer";
        this.Size = new Size(1000, 800);
        this.BackColor = Color.FromArgb(15, 15, 15);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        Panel header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(25, 25, 25) };
        this.Controls.Add(header);

        _fpsLabel = CreateHeaderLabel("FPS: 0", Color.Gray, new Font("Consolas", 11, FontStyle.Bold));
        _highScoreLabel = CreateHeaderLabel(GetBestString(), Color.Gray, new Font("Consolas", 11, FontStyle.Bold)); 
        _pointsLabel = CreateHeaderLabel($"POINTS: {_points}", Color.Gray, new Font("Consolas", 11, FontStyle.Bold));
        _hitsLabel = CreateHeaderLabel($"HITS: {_roundHits}", Color.Gray, new Font("Consolas", 11, FontStyle.Bold));
        _missesLabel = CreateHeaderLabel($"MISSES: {_roundMisses}", Color.Gray, new Font("Consolas", 11, FontStyle.Bold));

        header.Controls.AddRange(new Control[] { _missesLabel, _hitsLabel, _pointsLabel, _highScoreLabel, _fpsLabel });

        this.MouseDown += HandleGlobalClick;
        this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) ToggleMenu(); };

        AddNewTarget();
        _gameLoop = new System.Windows.Forms.Timer { Interval = 10 };
        _gameLoop.Tick += (s, e) => { if (!_isPaused) { UpdateGame(); this.Invalidate(); } };
        _gameLoop.Start();
        _stopwatch.Start();
        _fpsStopwatch.Start();
    }

    private void SetupMenu()
    {
        Color solidBack = Color.FromArgb(30, 30, 30);
        Size pSize = new Size(320, 380);

        _menuPanel = CreateBasePanel(pSize, solidBack, "");
        Panel mContent = (Panel)_menuPanel.Controls[0];
        
        mContent.Controls.Add(CreateMenuButton("LEADERBOARD", 45, (s, e) => MessageBox.Show("Coming Soon!", "")));
        mContent.Controls.Add(CreateMenuButton("SETTINGS", 115, (s, e) => { _menuPanel.Visible = false; _settingsPanel.Visible = true; }));
        mContent.Controls.Add(CreateMenuButton("RESET STATS", 185, (s, e) => ResetAllStats()));
        mContent.Controls.Add(CreateMenuButton("CREDITS", 255, (s, e) => { _menuPanel.Visible = false; _creditsPanel.Visible = true; }));
        
        _settingsPanel = CreateBasePanel(pSize, solidBack, "SETTINGS");
        Panel sContent = (Panel)_settingsPanel.Controls[0];
        CheckBox chkMarks = new CheckBox { 
            Text = "Show Marks", ForeColor = Color.White, 
            Checked = _showMarks, FlatStyle = FlatStyle.Flat, 
            Font = new Font("Consolas", 10, FontStyle.Bold), 
            AutoSize = true, Location = new Point(25, 80) 
        };
        chkMarks.CheckedChanged += (s, e) => { _showMarks = chkMarks.Checked; SaveGameData(); };
        sContent.Controls.Add(chkMarks);

        _creditsPanel = CreateBasePanel(pSize, solidBack, "CREDITS");
        Panel cContent = (Panel)_creditsPanel.Controls[0];
        Label cDev = new Label { 
            Text = "Developed by q1lra", ForeColor = Color.Gray, 
            Font = new Font("Consolas", 10), AutoSize = true, 
            Location = new Point(25, 80) 
        };
        cContent.Controls.Add(cDev);

        this.Controls.AddRange(new Control[] { _menuPanel, _settingsPanel, _creditsPanel });
    }

    private void ResetAllStats()
    {
        if (MessageBox.Show("Reset everything?", "", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            _points = 0; _streak = 0; _roundHits = 0; _roundMisses = 0; _highestHits = 0; _bestStreak = 0; _bestScore = long.MaxValue; _missCount = 0;
            _wallMarkers.Clear();
            _targets.Clear();
            AddNewTarget();
            UpdateStats();
            SaveGameData();
        }
    }

    private Panel CreateBasePanel(Size size, Color backColor, string title)
    {
        Panel border = new Panel { Size = size, BackColor = Color.FromArgb(100, 100, 100), Padding = new Padding(1), Visible = false, Location = new Point((1000 - size.Width) / 2, (800 - size.Height) / 2) };
        Panel content = new Panel { Dock = DockStyle.Fill, BackColor = backColor };
        border.Controls.Add(content);
        if (!string.IsNullOrEmpty(title)) {
            Label lbl = new Label { Text = title, ForeColor = Color.White, Font = new Font("Consolas", 13, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            content.Controls.Add(lbl);
        }
        Label xBtn = new Label { Text = "✕", ForeColor = Color.White, Font = new Font("Arial", 12, FontStyle.Bold), Size = new Size(30, 30), Location = new Point(size.Width - 35, 5), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
        xBtn.Click += (s, e) => { if (string.IsNullOrEmpty(title)) ToggleMenu(); else { border.Visible = false; _menuPanel.Visible = true; } };
        content.Controls.Add(xBtn);
        return border;
    }

    private Button CreateMenuButton(string text, int y, EventHandler click)
    {
        var b = new Button { Text = text, Location = new Point(60, y), Size = new Size(200, 50), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Font = new Font("Consolas", 9, FontStyle.Bold) };
        b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
        b.Click += click; return b;
    }

    private void ToggleMenu()
    {
        _isPaused = !_isPaused;
        _menuPanel.Visible = _isPaused;
        _settingsPanel.Visible = _creditsPanel.Visible = false;
        if (!_isPaused) _stopwatch.Start(); else _stopwatch.Stop();
    }

    // Simplified as requested: ONLY shows best reaction time
    private string GetBestString() => $"BEST: {(_bestScore == long.MaxValue ? 0 : _bestScore)}ms";

    private void AddNewTarget() => _targets.Add(new Target { Pos = new PointF(_random.Next(100, 800), _random.Next(150, 600)), VX = 2, VY = 2, TVX = 2, TVY = 2 });

    private void HandleGlobalClick(object? sender, MouseEventArgs e)
    {
        if (e.Y < 50 || _isPaused) return;
        Target? hit = null;
        foreach (var t in _targets)
            if (Math.Sqrt(Math.Pow(e.X - (t.Pos.X + t.Radius), 2) + Math.Pow(e.Y - (t.Pos.Y + t.Radius), 2)) <= t.Radius) { hit = t; break; }

        if (hit != null) {
            long res = _stopwatch.ElapsedMilliseconds;
            if (res < 5) return;
            
            _roundHits++; _points += 2; // Hit rewards 2 points
            _streak++;
            
            PointF GetRandomPos() => new PointF(e.X + _random.Next(-35, 35), e.Y + _random.Next(-50, 0));

            if (res < _bestScore) {
                _bestScore = res;
                _floatingTexts.Add(new FloatingText { Text = "TOP SCORE", Location = GetRandomPos(), Color = Color.Yellow });
            }

            if (res < 140) {
                _floatingTexts.Add(new FloatingText { Text = "NON-HUMAN", Location = GetRandomPos(), Color = Color.Yellow });
            }
            else if (res < 180) {
                _floatingTexts.Add(new FloatingText { Text = "LUCKY", Location = GetRandomPos(), Color = Color.Yellow });
            }
            else if (res <= 250) {
                _floatingTexts.Add(new FloatingText { Text = "HIGH SCORE", Location = GetRandomPos(), Color = Color.Yellow });
            }

            _floatingTexts.Add(new FloatingText { Text = $"{_streak}x", Location = new PointF(e.X + _random.Next(-10, 10), e.Y + _random.Next(-10, 10)), Color = Color.LimeGreen });

            if (_roundHits > _highestHits) _highestHits = _roundHits;
            
            if (_roundHits > 0 && _roundHits % 20 == 0) AddNewTarget();

            _wallMarkers.Add(new WallMarker { Location = e.Location, Type = MarkerType.HitX });
            hit.Pos = new PointF(_random.Next(100, 800), _random.Next(150, 600));
            _stopwatch.Restart();
        } else {
            _points -= 10; _missCount++; _roundMisses++; // Miss removes 10 points
            _wallMarkers.Add(new WallMarker { Location = e.Location, Type = MarkerType.BulletHole });
            
            _floatingTexts.Add(new FloatingText { Text = $"miss ({_missCount}/3)", Location = new PointF(e.X + _random.Next(-20, 20), e.Y), Color = Color.Red });
            _floatingTexts.Add(new FloatingText { Text = "-10", Location = new PointF(e.X + _random.Next(-20, 20), e.Y + 15), Color = Color.Red });

            if (_missCount >= 3) {
                _streak = 0;
                _missCount = 0;
                _roundHits = 0;
                _roundMisses = 0;
                if (_targets.Count > 1) _targets.RemoveRange(1, _targets.Count - 1);
            }
        }
        UpdateStats(); SaveGameData();
    }

    private void UpdateGame()
    {
        // FPS Calculation
        _frameCount++;
        if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
        {
            _fps = _frameCount;
            _frameCount = 0;
            _fpsStopwatch.Restart();
            _fpsLabel.Text = $"FPS: {_fps}";
        }

        for (int i = _floatingTexts.Count - 1; i >= 0; i--) {
            _floatingTexts[i].Opacity -= 0.012f; _floatingTexts[i].Location.Y -= 0.4f;
            if (_floatingTexts[i].Opacity <= 0) _floatingTexts.RemoveAt(i);
        }

        float speed = Math.Min(1.2f * (1.0f + (_roundHits * 0.015f)), 6.5f);
        foreach (var t in _targets) {
            if (_random.Next(0, 60) == 0) { t.TVX = (float)((_random.NextDouble() * 2 - 1) * speed); t.TVY = (float)((_random.NextDouble() * 2 - 1) * speed); }
            t.VX += (t.TVX - t.VX) * 0.05f; t.VY += (t.TVY - t.VY) * 0.05f;
            t.Pos.X += t.VX; t.Pos.Y += t.VY;
            if (t.Pos.X <= 0) { t.VX = Math.Abs(t.VX); t.Pos.X = 0; }
            if (t.Pos.X >= ClientSize.Width - (t.Radius * 2)) { t.VX = -Math.Abs(t.VX); t.Pos.X = ClientSize.Width - (t.Radius * 2); }
            if (t.Pos.Y <= 50) { t.VY = Math.Abs(t.VY); t.Pos.Y = 50; }
            if (t.Pos.Y >= ClientSize.Height - (t.Radius * 2)) { t.VY = -Math.Abs(t.VY); t.Pos.Y = ClientSize.Height - (t.Radius * 2); }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        string hint = "PRESS [ESC] TO OPEN MENU";
        using (Font hf = new Font("Consolas", 8, FontStyle.Bold)) {
            SizeF sz = g.MeasureString(hint, hf);
            g.DrawString(hint, hf, Brushes.DimGray, (this.ClientSize.Width - sz.Width) / 2, 60);
        }
        if (_showMarks) {
            foreach (var m in _wallMarkers) {
                if (m.Type == MarkerType.HitX) {
                    using var p = new Pen(Color.FromArgb(80, 200, 0, 0), 1.5f);
                    g.DrawLine(p, m.Location.X - 4, m.Location.Y - 4, m.Location.X + 4, m.Location.Y + 4);
                    g.DrawLine(p, m.Location.X + 4, m.Location.Y - 4, m.Location.X - 4, m.Location.Y + 4);
                } else {
                    using var bR = new SolidBrush(Color.FromArgb(160, 45, 45, 45));
                    using var bC = new SolidBrush(Color.FromArgb(200, 15, 15, 15));
                    g.FillEllipse(bR, m.Location.X - 3.5f, m.Location.Y - 3.5f, 7, 7);
                    g.FillEllipse(bC, m.Location.X - 1.5f, m.Location.Y - 1.5f, 3, 3);
                }
            }
        }
        foreach (var t in _targets) {
            g.FillEllipse(Brushes.LightSteelBlue, t.Pos.X, t.Pos.Y, t.Radius * 2, t.Radius * 2);
            g.DrawEllipse(Pens.White, t.Pos.X, t.Pos.Y, t.Radius * 2, t.Radius * 2);
        }
        foreach (var ft in _floatingTexts) {
            using var b = new SolidBrush(Color.FromArgb((int)(ft.Opacity * 255), ft.Color));
            g.DrawString(ft.Text, new Font("Consolas", 9, FontStyle.Bold), b, ft.Location);
        }
    }

    private Label CreateHeaderLabel(string t, Color c, Font f) => new Label { Text = t, ForeColor = c, Font = f, Dock = DockStyle.Left, Width = 190, TextAlign = ContentAlignment.MiddleCenter };
    private void UpdateStats() { 
        _hitsLabel.Text = $"HITS: {_roundHits}"; 
        _missesLabel.Text = $"MISSES: {_roundMisses}"; 
        _pointsLabel.Text = $"POINTS: {_points}"; 
        _highScoreLabel.Text = GetBestString(); 
    }

    private void SaveGameData() {
        try { File.WriteAllBytes(_savePath, EncryptString($"{_bestScore}|{_points}|{_highestHits}|{_bestStreak}|{_showMarks}")); } catch { }
    }

    private void LoadGameData() {
        try {
            if (!File.Exists(_savePath)) return;
            string[] p = DecryptString(File.ReadAllBytes(_savePath)).Split('|');
            _bestScore = long.Parse(p[0]); 
            _points = int.Parse(p[1]); 
            _highestHits = int.Parse(p[2]); 
            _bestStreak = int.Parse(p[3]);
            if (p.Length >= 5) _showMarks = bool.Parse(p[4]);
        } catch { }
    }

    private byte[] EncryptString(string t) {
        using Aes a = Aes.Create(); a.Key = Key; a.IV = IV;
        return a.CreateEncryptor().TransformFinalBlock(Encoding.UTF8.GetBytes(t), 0, t.Length);
    }

    private string DecryptString(byte[] d) {
        using Aes a = Aes.Create(); a.Key = Key; a.IV = IV;
        return Encoding.UTF8.GetString(a.CreateDecryptor().TransformFinalBlock(d, 0, d.Length));
    }
}