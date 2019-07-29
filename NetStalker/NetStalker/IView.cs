﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BrightIdeasSoftware;
using MaterialSkin.Controls;

namespace CSArp
{
    public interface IView
    {
        FastObjectListView ListView1 { get; }
        Form MainForm { get; }
        MaterialLabel StatusLabel { get; }
        PictureBox PictureBox { get; }
        PictureBox LoadingBar { get; }
        MaterialLabel StatusLabel2 { get; }
        ToolTip TTip { get; }

    }
}
