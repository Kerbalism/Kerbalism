import yaml
f = open("CHANGELOG.md", "r");
a = f.read();
f.close();

entries = a.split(
    '------------------------------------------------------------------------------------------------------');
final = dict();

for c in entries:
	d = c.split("\n");
	name = ''
	cl = list()
	date = ''
	for f in d:
		if f.startswith('## '):
			name = f.replace('## ', '');
			print('Name is: ' + name);
		if f.startswith(' - '):
			date = f.replace(' - ', '');
			print('Date is: ' + date);
		if f.startswith('* '):
			dd = f.replace('* ', '');
			cl.append(dd);
			print('Entry: ' + dd);
		if f.startswith(' * '):
			dd = f.replace(' * ', '');
			cl.append(dd);
			print('Entry: ' + dd);
	final[name] = dict(
		date = date,
		changes = cl
	)

with open('data.yml', 'w') as outfile:
    yaml.dump(final, outfile, default_flow_style=False)