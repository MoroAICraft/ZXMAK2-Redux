using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ZXMAK2.Hardware.Circuits.Sound;
class PsgAssemblyAudit {
    static string Hash(byte[] b) {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","");}
    static void Main(string[] args) {
        var asm=typeof(PsgChip).Assembly;
        using(var w=new StreamWriter(args[0]))foreach(var type in asm.GetTypes().OrderBy(t=>t.FullName)) {
            var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly;
            foreach(var m in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)).OrderBy(m=>m.ToString())) {
                var body=m.GetMethodBody();w.WriteLine(type.FullName+"|"+m+"|"+(body==null?"abstract":Hash(body.GetILAsByteArray())));
            }
        }
        using(var w=new StreamWriter(args[1])) {
            w.WriteLine(asm.FullName);
            foreach(var t in asm.GetExportedTypes().OrderBy(t=>t.FullName)) {
                w.WriteLine(t.FullName+" : "+t.BaseType);
                foreach(var m in t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).OrderBy(m=>m.ToString()))w.WriteLine(m.MemberType+"|"+m);
            }
        }
    }
}
