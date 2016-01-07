import os
import sys


def GenerateSources(path, name, count):
    print "*", name, count
    template = open(os.path.join(path, name) + ".cs").read()
    for i in range(count):
        new_name = "X_{0}_{1:04d}".format(name, i)
        src = template.replace(name, new_name)
        with open(os.path.join(path, new_name) + ".cs", "wb") as f:
            f.write(src)


def main(editor_count, plugins_count, scripts_count):
    GenerateSources(r'./Assets/Editor/Dummy', 'EditorDummy', editor_count)
    GenerateSources(r'./Assets/Plugins/Dummy', 'PluginsDummy', plugins_count)
    GenerateSources(r'./Assets/Scripts/Dummy', 'ScriptsDummy', scripts_count)


if len(sys.argv) != 4:
    print "[Usage]",
    print os.path.split(sys.argv[0])[1], "editor_count plugins_count scripts_count"
else:
    counts = [int(v) for v in sys.argv[1:]]
    main(*counts)    
