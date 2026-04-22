using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Text; // Required for Encoding

namespace ReactionTrainer;

public partial class Form1 : Form
{
    private Stopwatch _stopwatch = new Stopwatch();
    private Random _random = new Random();
    private Button _target = null!; 
    private Label _statsLabel = null!;
    private Label _highScoreLabel = null!;
    private System.Windows.Forms.Timer _timeoutTimer = null!;
    
    private long _bestScore = long.MaxValue;
    private string _saveDir;
    private string _saveFile;
    private const int _timeoutLimit = 2000; 
    private bool _isPaused = true; 

    public Form1()
    {
        _saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReactionTrainer");
        _saveFile = Path.Combine(_saveDir, "user.dat");

        LoadHighScore();
        SetupGame();
        
        this.Shown += (s, e) => {
            this.Activate();
            this.Focus();
        };
    }

    private void SetupGame()
    {
        this.Text = "Reaction Trainer";
        this.Size = new Size(800, 600);
        this.BackColor = Color.FromArgb(20, 20, 20);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        this.MouseDown += Form1_MouseDown;

        _statsLabel = new Label
        {
            Text = "---", 
            ForeColor = Color.DimGray,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Consolas", 14, FontStyle.Bold)
        };

        _highScoreLabel = new Label
        {
            Text = _bestScore == long.MaxValue ? "Best: --" : $"Best: {_bestScore}ms",
            ForeColor = Color.Gold,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _target = new Button
        {
            Size = new Size(26, 26),
            BackColor = Color.Red,
            FlatStyle = FlatStyle.Flat
        };
        _target.FlatAppearance.BorderSize = 0;
        _target.Paint += Target_Paint;
        _target.MouseDown += Target_MouseDown;

        _timeoutTimer = new System.Windows.Forms.Timer();
        _timeoutTimer.Interval = _timeoutLimit;
        _timeoutTimer.Tick += (s, e) => HandleTimeout();
        
        this.Controls.Add(_highScoreLabel);
        this.Controls.Add(_statsLabel);
        this.Controls.Add(_target);
        
        MoveTarget();
    }

    private void Target_MouseDown(object? sender, MouseEventArgs e)
    {
        _timeoutTimer.Stop();

        if (_isPaused)
        {
            _isPaused = false;
            _statsLabel.ForeColor = Color.White;
        }
        else if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            long currentResult = _stopwatch.ElapsedMilliseconds;

            if (currentResult < _timeoutLimit)
            {
                _statsLabel.Text = $"{currentResult}ms";
                _statsLabel.ForeColor = Color.LimeGreen;

                if (currentResult < _bestScore)
                {
                    _bestScore = currentResult;
                    _highScoreLabel.Text = $"Best: {_bestScore}ms";
                    SaveHighScore();
                }
            }
        }

        MoveTarget();
        StartNewRound();
    }

    private void Form1_MouseDown(object? sender, MouseEventArgs e)
    {
        if (!_isPaused && _stopwatch.IsRunning)
        {
            _statsLabel.Text = "MISS";
            _statsLabel.ForeColor = Color.OrangeRed;
        }
    }

    private void HandleTimeout()
    {
        _timeoutTimer.Stop();
        _stopwatch.Reset();
        _isPaused = true;
        _statsLabel.Text = "---";
        _statsLabel.ForeColor = Color.DimGray;
    }

    private void StartNewRound()
    {
        _stopwatch.Restart();
        _timeoutTimer.Start();
    }

    private void MoveTarget()
    {
        int maxX = this.ClientSize.Width - _target.Width;
        int maxY = this.ClientSize.Height - _target.Height - _statsLabel.Height - _highScoreLabel.Height;
        int nextX = _random.Next(0, maxX);
        int nextY = _random.Next(_statsLabel.Height + _highScoreLabel.Height, maxY);
        _target.Location = new Point(nextX, nextY);
    }

    private void Target_Paint(object? sender, PaintEventArgs e)
    {
        GraphicsPath path = new GraphicsPath();
        path.AddEllipse(0, 0, _target.Width, _target.Height);
        _target.Region = new Region(path);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
    }

    // --- ENCODED SAVE/LOAD ---

    private void SaveHighScore()
    {
        try 
        {
            if (!Directory.Exists(_saveDir)) Directory.CreateDirectory(_saveDir);
            
            // Convert score to a byte array and then to a Base64 string
            byte[] textBytes = Encoding.UTF8.GetBytes(_bestScore.ToString());
            string encodedData = Convert.ToBase64String(textBytes);
            
            File.WriteAllText(_saveFile, encodedData);
        } 
        catch { }
    }

    private void LoadHighScore()
    {
        if (File.Exists(_saveFile))
        {
            try 
            {
                string encodedData = File.ReadAllText(_saveFile);
                
                // Decode the Base64 string back to the original score string
                byte[] decodedBytes = Convert.FromBase64String(encodedData);
                string decodedText = Encoding.UTF8.GetString(decodedBytes);

                if (long.TryParse(decodedText, out long savedScore))
                {
                    _bestScore = savedScore;
                }
            } 
            catch { _bestScore = long.MaxValue; }
        }
    }
}