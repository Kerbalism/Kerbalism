from cfgnode import ConfigNode
import sys
import yaml

file = open(sys.argv[1], 'r')
data = file.read()
file.close()

cfg = ConfigNode()
yml = list(yaml.load_all(data))

cl_node = cfg.AddNewNode('KERBALCHANGELOG')
cl_node.AddValue('showChangelog', True)
cl_node.AddValue('modName', 'Kerbalism')
for a in yml:
    for _, b in a.items():
        for k, v in b.items():  # i hate python
            c = cl_node.AddNewNode('VERSION')
            c.AddValue('version', k)
            for change in v['changes']:
                c.AddValue('change', change)
print('KERBALCHANGELOG\n' + cl_node.ToString())

file = open(sys.argv[2], 'w')
file.write('KERBALCHANGELOG\n' + cl_node.ToString())
file.close()