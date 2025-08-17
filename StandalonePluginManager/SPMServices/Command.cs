using ECommons.SimpleGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandalonePluginManager.SPMServices;
public unsafe sealed class Command
{
    private Command()
    {
        EzCmd.Add("/spm", EzConfigGui.Open);
    }
}