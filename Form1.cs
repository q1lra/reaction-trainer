using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;

namespace ReactionTrainer;

public partial class Form1 : Form
{
    private Stopwatch _stopwatch = new Stopwatch();
    private Random _random = new Random();
    private Button _target = null!;
    private Panel _bgPanel = null!; 
    private Label _statsLabel = null!, _highScoreLabel = null!, _pointsLabel = null!, _streakLabel = null!;
    
    private System.Windows.Forms.Timer _animationTimer = null!;

    private float _velocityX = 1.2f, _velocityY = 1.0f;
    private float _targetVelocityX = 1.2f, _targetVelocityY = 1.0f;
    private float _acceleration = 0.04f; 
    
    private int _points = 0, _streak = 0;
    private long _bestScore = long.MaxValue;

    private string _saveDir, _saveFile, _pointsFile;

    private enum MarkerType { HitX, BulletHole }
    private class WallMarker { public Point Location; public MarkerType Type; public float Rotation; }
    private List<WallMarker> _wallMarkers = new();

    public Form1()
    {
        _saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReactionTrainer");
        if (!Directory.Exists(_saveDir)) Directory.CreateDirectory(_saveDir);
        _saveFile = Path.Combine(_saveDir, "user.dat");
        _pointsFile = Path.Combine(_saveDir, "points.dat");

        LoadHighScore();
        LoadPoints();
        SetupGame();
    }

    private void SetupGame()
    {
        this.Text = "Reaction Trainer";
        this.Size = new Size(1000, 800);
        this.BackColor = Color.FromArgb(20, 20, 20);
        this.DoubleBuffered = true;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        _bgPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _bgPanel.MouseDown += (s, e) => { HandleMiss(e.Location); };
        _bgPanel.Paint += (s, e) => { DrawWallMarkers(e.Graphics); };
        this.Controls.Add(_bgPanel);

        Color themeGrey = Color.FromArgb(140, 140, 140);
        _statsLabel = CreateLabel(45, 20, themeGrey, "GO");
        _highScoreLabel = CreateLabel(30, 10, themeGrey, _bestScore == long.MaxValue ? "BEST: --" : $"BEST: {_bestScore}ms");
        _pointsLabel = CreateLabel(30, 10, themeGrey, $"POINTS: {_points}");
        _streakLabel = CreateLabel(30, 10, themeGrey, $"STREAK: {_streak}");

        _target = new Button {
            Size = new Size(48, 48),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Cross,
            TabStop = false 
        };
        _target.FlatAppearance.BorderSize = 0;
        _target.FlatAppearance.CheckedBackColor = Color.Transparent;
        _target.FlatAppearance.MouseDownBackColor = Color.Transparent;
        _target.FlatAppearance.MouseOverBackColor = Color.Transparent;
        
        _target.Paint += (s, e) => {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(Color.FromArgb(180, 173, 216, 230));
            e.Graphics.FillEllipse(b, 4, 4, 40, 40);
            using var p = new Pen(Color.White, 1);
            e.Graphics.DrawEllipse(p, 4, 4, 40, 40);
        };
        
        _target.MouseDown += (s, e) => { ProcessHit(); };

        _animationTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _animationTimer.Tick += AnimationTimer_Tick;
        _animationTimer.Start();

        _bgPanel.Controls.Add(_streakLabel);
        _bgPanel.Controls.Add(_pointsLabel);
        _bgPanel.Controls.Add(_highScoreLabel);
        _bgPanel.Controls.Add(_statsLabel);
        _bgPanel.Controls.Add(_target);

        _target.Location = new Point(476, 376);
        _stopwatch.Start();
    }

    private Label CreateLabel(int h, int f, Color c, string t) => new Label { Text = t, ForeColor = c, Dock = DockStyle.Top, Height = h, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Consolas", f, FontStyle.Bold), AutoSize = false, BackColor = Color.Transparent };

    private void HandleMiss(Point loc)
    {
        _streak = 0;
        _statsLabel.Text = "MISS";
        _wallMarkers.Add(new WallMarker { Location = loc, Type = MarkerType.BulletHole, Rotation = _random.Next(360) });
        UpdateStats();
        _bgPanel.Invalidate();
    }

    private void ProcessHit()
    {
        _stopwatch.Stop();
        long res = _stopwatch.ElapsedMilliseconds;
        _statsLabel.Text = $"{res}ms";
        _points++; 
        _streak++;
        
        SavePoints(); 
        UpdateStats();
        _wallMarkers.Add(new WallMarker { Location = new Point(_target.Location.X + 24, _target.Location.Y + 24), Type = MarkerType.HitX });
        
        if (res < _bestScore) { _bestScore = res; SaveHighScore(); }
        _stopwatch.Restart(); 
        MoveTarget();
        _bgPanel.Invalidate();
        this.ActiveControl = null; 
    }

    private void DrawWallMarkers(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var marker in _wallMarkers) {
            if (marker.Type == MarkerType.HitX) {
                // SMALLER & MORE TRANSPARENT RED X
                using var pen = new Pen(Color.FromArgb(70, 220, 0, 0), 1);
                int s = 5; 
                g.DrawLine(pen, marker.Location.X - s, marker.Location.Y - s, marker.Location.X + s, marker.Location.Y + s);
                g.DrawLine(pen, marker.Location.X + s, marker.Location.Y - s, marker.Location.X - s, marker.Location.Y + s);
            } 
            else {
                // SOLID BULLET HOLE
                using var bCore = new SolidBrush(Color.Black);
                using var bRim = new SolidBrush(Color.FromArgb(40, 40, 40));
                g.FillEllipse(bRim, marker.Location.X - 3, marker.Location.Y - 3, 6, 6);
                g.FillEllipse(bCore, marker.Location.X - 2, marker.Location.Y - 2, 4, 4);
            }
        }
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        float max = Math.Min(1.8f + (_streak * 0.04f), 8f);
        if (_random.Next(0, 60) == 0) {
            _targetVelocityX = (float)((_random.NextDouble() * 2 - 1) * max);
            _targetVelocityY = (float)((_random.NextDouble() * 2 - 1) * (max * 0.6f));
        }
        if (_velocityX < _targetVelocityX) _velocityX += _acceleration; else _velocityX -= _acceleration;
        if (_velocityY < _targetVelocityY) _velocityY += _acceleration; else _velocityY -= _acceleration;
        _target.Left += (int)_velocityX; _target.Top += (int)_velocityY;

        if (_target.Left <= 0 || _target.Left >= ClientSize.Width - _target.Width) { _targetVelocityX *= -1; _velocityX *= -0.8f; }
        if (_target.Top <= 180 || _target.Top >= ClientSize.Height - _target.Height) { _targetVelocityY *= -1; _velocityY *= -0.8f; }
    }

    private void MoveTarget() => _target.Location = new Point(_random.Next(50, Width - 100), _random.Next(200, Height - 150));

    private void UpdateStats() { 
        _streakLabel.Text = $"STREAK: {_streak}"; 
        _pointsLabel.Text = $"POINTS: {_points}"; 
        _highScoreLabel.Text = _bestScore == long.MaxValue ? "BEST: --" : $"BEST: {_bestScore}ms"; 
    }

    private void SaveHighScore() { try { File.WriteAllText(_saveFile, Convert.ToBase64String(Encoding.UTF8.GetBytes(_bestScore.ToString()))); } catch {} }
    private void LoadHighScore() { try { if (File.Exists(_saveFile)) _bestScore = long.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(_saveFile)))); } catch {} }
    private void SavePoints() { try { File.WriteAllText(_pointsFile, Convert.ToBase64String(Encoding.UTF8.GetBytes(_points.ToString()))); } catch {} }
    private void LoadPoints() { try { if (File.Exists(_pointsFile)) _points = int.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(_pointsFile)))); } catch {} }
}