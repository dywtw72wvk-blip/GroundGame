"""Compile the actual controller body with a fake engine and run behavioral checks."""
from pathlib import Path
import argparse
import os
import subprocess

ROOT = Path(__file__).resolve().parents[1]
COMPILER = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--game',type=Path,default=Path(r'Z:\SteamLibrary\steamapps\common\SpaceEngineers'))
    args=parser.parse_args()
    out=ROOT/'output/missile'; out.mkdir(parents=True,exist_ok=True)
    source=(ROOT/'src/missile/Program.cs').read_text(encoding='utf-8')
    source=source.replace('public sealed class Program : MyGridProgram','public sealed class Program : FakeGridProgram')
    (out/'ControllerUnderTest.cs').write_text(source,encoding='utf-8')
    refs=['Sandbox.Common','Sandbox.Game','SpaceEngineers.Game','VRage','VRage.Game','VRage.Library','VRage.Math','VRage.Scripting','netstandard']
    subprocess.run([COMPILER,'/nologo','/out:'+str(out/'MissileHarness.exe')]+
        ['/reference:'+str(args.game/'Bin64'/(n+'.dll')) for n in refs]+
        [str(out/'ControllerUnderTest.cs'),str(ROOT/'tests/MissileHarness.cs')],check=True)
    # Keep proprietary game DLLs in the game directory; resolve them at runtime.
    launcher='''using System; using System.IO; using System.Reflection;
class Launch { static int Main(string[] args) {
AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
 string p=Path.Combine(args[0],new AssemblyName(e.Name).Name+".dll");
 return File.Exists(p)?Assembly.LoadFrom(p):null; };
try { Assembly.LoadFrom(args[1]).EntryPoint.Invoke(null,null); return 0; }
catch(Exception e) { Console.Error.WriteLine(e); return 1; }
} }'''
    (out/'Launch.cs').write_text(launcher)
    subprocess.run([COMPILER,'/nologo','/out:'+str(out/'Launch.exe'),str(out/'Launch.cs')],check=True)
    subprocess.run([str(out/'Launch.exe'),str(args.game/'Bin64'),str(out/'MissileHarness.exe')],check=True)
    subprocess.run([COMPILER,'/nologo','/out:'+str(out/'MissileBlueprintLoad.exe')]+
        ['/reference:'+str(args.game/'Bin64'/(n+'.dll')) for n in refs]+
        [str(ROOT/'tests/MissileBlueprintLoad.cs')],check=True)
    env=dict(os.environ,GROUNDGAME_ROOT=str(ROOT))
    subprocess.run([str(out/'Launch.exe'),str(args.game/'Bin64'),str(out/'MissileBlueprintLoad.exe')],env=env,check=True)

if __name__=='__main__': main()
