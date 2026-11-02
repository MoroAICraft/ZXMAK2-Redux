using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ZXMAK2.Engine.Cpu;

class NmiAssemblyAudit
{
    static string Hash(byte[] bytes)
    {
        using(var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
    static void Main(string[] args)
    {
        var asm = typeof(CpuUnit).Assembly;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
            BindingFlags.Instance | BindingFlags.DeclaredOnly;
        using(var writer = new StreamWriter(args[0])) foreach(var type in asm.GetTypes().OrderBy(t => t.FullName))
            foreach(var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)).OrderBy(m => m.ToString()))
            {
                var body = method.GetMethodBody();
                writer.WriteLine(type.FullName + "|" + method + "|" + (body == null ? "abstract" : Hash(body.GetILAsByteArray())));
            }
        using(var writer = new StreamWriter(args[1]))
        {
            writer.WriteLine(asm.FullName);
            foreach(var type in asm.GetExportedTypes().OrderBy(t => t.FullName))
            {
                writer.WriteLine(type.FullName + " : " + type.BaseType);
                foreach(var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                    BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.ToString()))
                    writer.WriteLine(member.MemberType + "|" + member);
            }
        }
    }
}
