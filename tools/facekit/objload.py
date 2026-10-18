import numpy as np
def load_obj(path):
    V=[];VT=[];F=[];FT=[];G=[];g=None
    for line in open(path):
        if line.startswith('v '):
            V.append([float(x) for x in line.split()[1:4]])
        elif line.startswith('vt '):
            VT.append([float(x) for x in line.split()[1:3]])
        elif line.startswith('g '):
            g=line.split()[1]
        elif line.startswith('f '):
            parts=line.split()[1:]
            vi=[int(p.split('/')[0])-1 for p in parts]
            ti=[int(p.split('/')[1])-1 if '/' in p and p.split('/')[1] else -1 for p in parts]
            F.append(vi);FT.append(ti);G.append(g)
    return np.array(V),np.array(VT),F,FT,G
def load_target(path,n=None):
    idx=[];d=[]
    for line in open(path):
        if line.startswith('#') or not line.strip(): continue
        p=line.split(); idx.append(int(p[0])); d.append([float(p[1]),float(p[2]),float(p[3])])
    return np.array(idx,dtype=np.int32),np.array(d,dtype=np.float32)
