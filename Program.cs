using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace VGAStudio
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private const int GridSize = 32;
        private const int CanvasPixels = 512;

        private readonly Color[] palette =
        {
            Color.FromArgb(0, 0, 0),       // 0 Black
            Color.FromArgb(0, 0, 170),     // 1 Blue
            Color.FromArgb(0, 170, 0),     // 2 Green
            Color.FromArgb(0, 170, 170),   // 3 Cyan
            Color.FromArgb(170, 0, 0),     // 4 Red
            Color.FromArgb(170, 0, 170),   // 5 Magenta
            Color.FromArgb(170, 85, 0),    // 6 Brown
            Color.FromArgb(170, 170, 170), // 7 Light Gray
            Color.FromArgb(85, 85, 85),    // 8 Dark Gray
            Color.FromArgb(85, 85, 255),   // 9 Bright Blue
            Color.FromArgb(85, 255, 85),   // 10 Bright Green
            Color.FromArgb(85, 255, 255),  // 11 Bright Cyan
            Color.FromArgb(255, 85, 85),   // 12 Bright Red
            Color.FromArgb(255, 85, 255), // 13 Bright Magenta
            Color.FromArgb(255, 255, 85), // 14 Yellow
            Color.FromArgb(255, 255, 255) // 15 White
        };

        private readonly string[] colorNames =
        {
            "Black", "Blue", "Green", "Cyan", "Red", "Magenta", "Brown",
            "Light Gray", "Dark Gray", "Bright Blue", "Bright Green",
            "Bright Cyan", "Bright Red", "Bright Magenta", "Yellow", "White"
        };

        private int[,] pixels = new int[GridSize, GridSize];
        private readonly Stack<int[,]> undoStack = new Stack<int[,]>();
        private int currentColor = 0;
        private string currentTool = "Pen";
        private bool showGrid = true;
        private bool drawing;
        private bool strokeStateSaved;

        private readonly Panel canvasPanel = new Panel();
        private readonly Panel palettePanel = new Panel();
        private readonly Panel previewBox = new Panel();
        private readonly Label previewLabel = new Label();
        private readonly Label coordinateLabel = new Label();
        private readonly Button penButton = new Button();
        private readonly Button fillButton = new Button();
        private readonly Button pickButton = new Button();
        private readonly Button gridButton = new Button();
        private readonly Button undoButton = new Button();

        private readonly Color backgroundColor = Color.FromArgb(255, 255, 255);
        private readonly Color panelColor = Color.FromArgb(255, 255, 255);
        private readonly Color borderColor = Color.FromArgb(61, 65, 77);
        private readonly Color textColor = Color.FromArgb(224, 226, 236);
        private readonly Color mutedColor = Color.FromArgb(140, 144, 158);
        private readonly Color accentColor = Color.FromArgb(85, 85, 255);

        public MainForm()
        {
            Text = "VGA Studio 32x32";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 650);
            MinimumSize = new Size(850, 620);
            BackColor = backgroundColor;
            ForeColor = textColor;
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;

            for (int y = 0; y < GridSize; y++)
                for (int x = 0; x < GridSize; x++)
                    pixels[y, x] = 15;

            BuildInterface();
            UpdatePreview();
            canvasPanel.Invalidate();
        }

        private void BuildInterface()
        {
            var title = new Label
            {
                Text = "VGA STUDIO",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 18)
            };
            var badge = new Label
            {
                Text = "32×32",
                ForeColor = Color.White,
                BackColor = accentColor,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4),
                Location = new Point(158, 22)
            };
            Controls.Add(title);
            Controls.Add(badge);

            var sidebar = new Panel
            {
                Location = new Point(20, 65),
                Size = new Size(260, 555),
                BackColor = panelColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(sidebar);

            int y = 15;
            AddSection(sidebar, "TOOLS", ref y);
            var toolFlow = new FlowLayoutPanel
            {
                Location = new Point(15, y),
                Size = new Size(226, 38),
                BackColor = panelColor,
                WrapContents = false
            };
            ConfigureToolButton(penButton, "Pen", true);
            ConfigureToolButton(fillButton, "Fill", false);
            ConfigureToolButton(pickButton, "Pick", false);
            penButton.Click += (s, e) => SetTool("Pen");
            fillButton.Click += (s, e) => SetTool("Fill");
            pickButton.Click += (s, e) => SetTool("Pick");
            toolFlow.Controls.AddRange(new Control[] { penButton, fillButton, pickButton });
            sidebar.Controls.Add(toolFlow);
            y += 55;

            AddSection(sidebar, "CURRENT COLOR", ref y);
            var preview = new Panel
            {
                Location = new Point(15, y),
                Size = new Size(226, 48),
                BackColor = backgroundColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            previewBox.Location = new Point(9, 9);
            previewBox.Size = new Size(28, 28);
            previewBox.BorderStyle = BorderStyle.FixedSingle;
            preview.Controls.Add(previewBox);
            previewLabel.Location = new Point(48, 14);
            previewLabel.AutoSize = true;
            previewLabel.Font = new Font("Consolas", 9F);
            previewLabel.ForeColor = textColor;
            preview.Controls.Add(previewLabel);
            sidebar.Controls.Add(preview);
            y += 63;

            AddSection(sidebar, "16-COLOR VGA PALETTE", ref y);
            palettePanel.Location = new Point(15, y);
            palettePanel.Size = new Size(226, 112);
            palettePanel.BackColor = panelColor;
            sidebar.Controls.Add(palettePanel);
            BuildPalette();
            y += 128;

            AddSection(sidebar, "CONTROLS", ref y);
            var controlFlow = new FlowLayoutPanel
            {
                Location = new Point(15, y),
                Size = new Size(226, 70),
                BackColor = panelColor
            };
            ConfigureButton(undoButton, "Undo", 106, 30);
            undoButton.Click += (s, e) => Undo();
            controlFlow.Controls.Add(undoButton);
            ConfigureButton(gridButton, "Grid: ON", 106, 30);
            gridButton.Click += (s, e) =>
            {
                showGrid = !showGrid;
                gridButton.Text = "Grid: " + (showGrid ? "ON" : "OFF");
                canvasPanel.Invalidate();
            };
            controlFlow.Controls.Add(gridButton);
            var clearButton = new Button();
            ConfigureButton(clearButton, "Clear Canvas", 218, 30);
            clearButton.Click += (s, e) => ClearCanvas();
            controlFlow.Controls.Add(clearButton);
            sidebar.Controls.Add(controlFlow);
            y += 88;

            AddSection(sidebar, "FILE OPERATIONS (.CSV)", ref y);
            var saveButton = new Button();
            ConfigureButton(saveButton, "Save CSV File", 226, 34);
            saveButton.BackColor = Color.FromArgb(35, 99, 58);
            saveButton.FlatAppearance.BorderColor = Color.FromArgb(46, 132, 78);
            saveButton.Click += (s, e) => SaveCsv();
            saveButton.Location = new Point(15, y);
            sidebar.Controls.Add(saveButton);
            var loadButton = new Button();
            ConfigureButton(loadButton, "Load CSV File", 226, 34);
            loadButton.BackColor = Color.FromArgb(40, 76, 125);
            loadButton.FlatAppearance.BorderColor = Color.FromArgb(53, 100, 165);
            loadButton.Click += (s, e) => LoadCsv();
            loadButton.Location = new Point(15, y + 42);
            sidebar.Controls.Add(loadButton);

            var canvasContainer = new Panel
            {
                Location = new Point(300, 65),
                Size = new Size(548, 580),
                BackColor = panelColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(canvasContainer);

            canvasPanel.Location = new Point(17, 17);
            canvasPanel.Size = new Size(CanvasPixels, CanvasPixels);
            canvasPanel.BackColor = Color.White;
            canvasPanel.Cursor = Cursors.Cross;
            canvasPanel.Paint += DrawCanvas;
            canvasPanel.MouseDown += CanvasMouseDown;
            canvasPanel.MouseMove += CanvasMouseMove;
            canvasPanel.MouseUp += (s, e) => { drawing = false; strokeStateSaved = false; };
            canvasPanel.MouseLeave += (s, e) => coordinateLabel.Text = "X: --, Y: --";
            canvasContainer.Controls.Add(canvasPanel);

            coordinateLabel.Location = new Point(17, 540);
            coordinateLabel.Size = new Size(250, 22);
            coordinateLabel.ForeColor = mutedColor;
            coordinateLabel.Font = new Font("Consolas", 9F);
            canvasContainer.Controls.Add(coordinateLabel);
            var standardLabel = new Label
            {
                Text = "Standard VGA 16-Color",
                Location = new Point(340, 540),
                Size = new Size(190, 22),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = mutedColor,
                Font = new Font("Consolas", 9F)
            };
            canvasContainer.Controls.Add(standardLabel);
        }

        private void AddSection(Control parent, string text, ref int y)
        {
            var label = new Label
            {
                Text = text,
                Location = new Point(15, y),
                AutoSize = true,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            parent.Controls.Add(label);
            y += 23;
        }

        private void ConfigureButton(Button button, string text, int width, int height)
        {
            button.Text = text;
            button.Size = new Size(width, height);
            button.Margin = new Padding(0, 0, 6, 6);
            button.BackColor = Color.FromArgb(52, 55, 64);
            button.ForeColor = textColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(66, 70, 82);
            button.Font = new Font("Segoe UI", 8.5F);
            button.Cursor = Cursors.Hand;
        }

        private void ConfigureToolButton(Button button, string text, bool active)
        {
            ConfigureButton(button, text, 70, 32);
            button.BackColor = active ? accentColor : Color.FromArgb(52, 55, 64);
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(123, 123, 255) : borderColor;
        }

        private void BuildPalette()
        {
            palettePanel.Controls.Clear();
            for (int i = 0; i < 16; i++)
            {
                int index = i;
                var swatch = new Panel
                {
                    Location = new Point((i % 4) * 56, (i / 4) * 27),
                    Size = new Size(48, 22),
                    BackColor = palette[i],
                    BorderStyle = i == currentColor ? BorderStyle.Fixed3D : BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                    Tag = i
                };
                var number = new Label
                {
                    Text = i.ToString(CultureInfo.InvariantCulture),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    Padding = new Padding(0, 0, 3, 0),
                    Enabled = false
                };
                swatch.Controls.Add(number);
                swatch.Click += (s, e) => SelectColor(index);
                palettePanel.Controls.Add(swatch);
            }
        }

        private void SelectColor(int index)
        {
            currentColor = index;
            BuildPalette();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            previewBox.BackColor = palette[currentColor];
            previewLabel.Text = $"{ColorTranslator.ToHtml(palette[currentColor]).ToUpperInvariant()} [{currentColor}]";
        }

        private void SetTool(string tool)
        {
            currentTool = tool;
            SetToolButton(penButton, tool == "Pen");
            SetToolButton(fillButton, tool == "Fill");
            SetToolButton(pickButton, tool == "Pick");
        }

        private void SetToolButton(Button button, bool active)
        {
            button.BackColor = active ? accentColor : Color.FromArgb(52, 55, 64);
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(123, 123, 255) : borderColor;
        }

        private int[,] CopyPixels()
        {
            return (int[,])pixels.Clone();
        }

        private void PushState()
        {
            if (undoStack.Count >= 25)
            {
                var states = undoStack.Reverse().Skip(1).ToArray();
                undoStack.Clear();
                foreach (var state in states) undoStack.Push(state);
            }
            undoStack.Push(CopyPixels());
        }

        private void Undo()
        {
            if (undoStack.Count == 0) return;
            pixels = undoStack.Pop();
            canvasPanel.Invalidate();
        }

        private void ClearCanvas()
        {
            if (MessageBox.Show("Clear the entire canvas to white?", "VGA Studio",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            PushState();
            for (int y = 0; y < GridSize; y++)
                for (int x = 0; x < GridSize; x++)
                    pixels[y, x] = 15;
            canvasPanel.Invalidate();
        }

        private void DrawCanvas(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            float cell = (float)canvasPanel.ClientSize.Width / GridSize;
            using (var brush = new SolidBrush(Color.White))
            {
                for (int y = 0; y < GridSize; y++)
                    for (int x = 0; x < GridSize; x++)
                    {
                        brush.Color = palette[pixels[y, x]];
                        e.Graphics.FillRectangle(brush, x * cell, y * cell, cell + 0.5F, cell + 0.5F);
                    }
            }

            if (showGrid)
            {
                using (var pen = new Pen(Color.FromArgb(38, 0, 0, 0), 1))
                {
                    for (int i = 0; i <= GridSize; i++)
                    {
                        float p = i * cell;
                        e.Graphics.DrawLine(pen, p, 0, p, canvasPanel.ClientSize.Height);
                        e.Graphics.DrawLine(pen, 0, p, canvasPanel.ClientSize.Width, p);
                    }
                }
            }
        }

        private Point GetCell(Point point)
        {
            int x = Math.Max(0, Math.Min(GridSize - 1, point.X * GridSize / Math.Max(1, canvasPanel.ClientSize.Width)));
            int y = Math.Max(0, Math.Min(GridSize - 1, point.Y * GridSize / Math.Max(1, canvasPanel.ClientSize.Height)));
            return new Point(x, y);
        }

        private void CanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            drawing = true;
            strokeStateSaved = false;
            if (currentTool == "Pen")
            {
                PushState();
                strokeStateSaved = true;
            }
            ApplyTool(GetCell(e.Location));
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            Point cell = GetCell(e.Location);
            coordinateLabel.Text = $"X: {cell.X}, Y: {cell.Y}";
            if (drawing && e.Button == MouseButtons.Left && currentTool == "Pen")
                ApplyTool(cell);
        }

        private void ApplyTool(Point point)
        {
            int x = point.X, y = point.Y;
            if (currentTool == "Pen")
            {
                if (pixels[y, x] == currentColor) return;
                pixels[y, x] = currentColor;
                canvasPanel.Invalidate();
            }
            else if (currentTool == "Fill")
            {
                PushState();
                FloodFill(x, y, pixels[y, x], currentColor);
                canvasPanel.Invalidate();
            }
            else if (currentTool == "Pick")
            {
                SelectColor(pixels[y, x]);
                SetTool("Pen");
            }
        }

        private void FloodFill(int startX, int startY, int targetColor, int fillColor)
        {
            if (targetColor == fillColor) return;
            var queue = new Queue<Point>();
            queue.Enqueue(new Point(startX, startY));
            while (queue.Count > 0)
            {
                Point p = queue.Dequeue();
                if (p.X < 0 || p.X >= GridSize || p.Y < 0 || p.Y >= GridSize) continue;
                if (pixels[p.Y, p.X] != targetColor) continue;
                pixels[p.Y, p.X] = fillColor;
                queue.Enqueue(new Point(p.X + 1, p.Y));
                queue.Enqueue(new Point(p.X - 1, p.Y));
                queue.Enqueue(new Point(p.X, p.Y + 1));
                queue.Enqueue(new Point(p.X, p.Y - 1));
            }
        }

        private void SaveCsv()
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "pixel_art.csv",
                DefaultExt = "csv"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var lines = new List<string>();
                for (int y = 0; y < GridSize; y++)
                {
                    var row = new string[GridSize];
                    for (int x = 0; x < GridSize; x++)
                        row[x] = pixels[y, x].ToString(CultureInfo.InvariantCulture);
                    lines.Add(string.Join(",", row));
                }
                File.WriteAllLines(dialog.FileName,  lines);
            }
        }

        private void LoadCsv()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string[] lines = File.ReadAllLines(dialog.FileName)
                        .Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                    if (lines.Length != GridSize)
                        throw new InvalidDataException($"File has {lines.Length} rows instead of 32.");

                    int[,] newPixels = new int[GridSize, GridSize];
                    for (int y = 0; y < GridSize; y++)
                    {
                        string[] cells = lines[y].Split(',').Select(v => v.Trim()).ToArray();
                        if (cells.Length != GridSize)
                            throw new InvalidDataException($"Row {y + 1} has {cells.Length} columns instead of 32.");

                        for (int x = 0; x < GridSize; x++)
                        {
                            int colorIndex;
                            if (int.TryParse(cells[x], NumberStyles.Integer, CultureInfo.InvariantCulture, out colorIndex)
                                && colorIndex >= 0 && colorIndex <= 15)
                            {
                                newPixels[y, x] = colorIndex;
                            }
                            else
                            {
                                colorIndex = Array.FindIndex(palette, c =>
                                    string.Equals(ColorTranslator.ToHtml(c), cells[x], StringComparison.OrdinalIgnoreCase));
                                newPixels[y, x] = colorIndex >= 0 ? colorIndex : 15;
                            }
                        }
                    }

                    PushState();
                    pixels = newPixels;
                    canvasPanel.Invalidate();
                    MessageBox.Show("File loaded successfully!", "VGA Studio",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading CSV file: " + ex.Message, "VGA Studio",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.B) SetTool("Pen");
            if (e.KeyCode == Keys.F) SetTool("Fill");
            if (e.KeyCode == Keys.I) SetTool("Pick");
        }
    }
}
