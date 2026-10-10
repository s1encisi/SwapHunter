
"""Preserve exact flat colours from legacy Blender material graphs in glTF 2.
No random tint, guessed palette, texture flattening or mesh/animation edits.
"""
import json,struct,hashlib
def read_flat_palette(materials):
    palette={}
    for mat in materials:
        if not mat:continue
        candidates=[];principled=None;outputs=[];emissions=[];surfaceTypes=[]
        if mat.use_nodes and mat.node_tree:
            for node in mat.node_tree.nodes:
                if node.type=="OUTPUT_MATERIAL":
                    outputs.append({"name":node.name,"target":node.target,"active":node.is_active_output,
                                    "surface":[l.from_node.name for l in node.inputs["Surface"].links]})
                    surfaceTypes.extend(l.from_node.type for l in node.inputs["Surface"].links)
                if node.type=="EMISSION":
                    emissions.append({"node":node.name,"color":list(node.inputs["Color"].default_value),
                                      "strength":float(node.inputs["Strength"].default_value)})
                if node.type not in ("BSDF_PRINCIPLED","BSDF_DIFFUSE"):continue
                key="Base Color" if node.type=="BSDF_PRINCIPLED" else "Color";socket=node.inputs[key]
                if socket.is_linked:raise RuntimeError("Texture/procedural colour needs explicit handling: "+mat.name)
                candidates.append({"node":node.name,"type":node.type,"color":list(socket.default_value)})
                if node.type=="BSDF_PRINCIPLED":principled=node
        if candidates:
            rgba=candidates[0]["color"]
            if any(max(abs(a-b) for a,b in zip(rgba,c["color"]))>1e-6 for c in candidates):
                raise RuntimeError("Legacy material contains conflicting flat shader colours: "+mat.name)
        elif emissions and surfaceTypes and all(t=="EMISSION" for t in surfaceTypes):
            # The exporter already handles pure emission, including HDR strength.
            # Keep that material verbatim; never reinterpret emission as base colour.
            palette[mat.name]={"retainExportedEmission":True,"sourceEmissionNodes":emissions,
                               "materialOutputs":outputs}
            continue
        elif not mat.use_nodes:rgba=list(mat.diffuse_color)
        else:raise RuntimeError("No supported authored flat colour: "+mat.name)
        metallic=float(principled.inputs["Metallic"].default_value) if principled else 0.0
        roughness=float(principled.inputs["Roughness"].default_value) if principled else float(mat.roughness)
        palette[mat.name]={"rgbaLinear":rgba,"metallic":metallic,"roughness":roughness,
                           "sourceShaderNodes":candidates,"sourceViewportColor":list(mat.diffuse_color),"materialOutputs":outputs}
    return palette

def restore_glb_flat_colors(path,palette,output=None):
    from pathlib import Path
    path=Path(path);raw=path.read_bytes()
    if raw[:4]!=b"glTF":raise RuntimeError("Expected binary glTF")
    length,kind=struct.unpack_from("<II",raw,12)
    if kind!=0x4e4f534a:raise RuntimeError("Expected glTF JSON chunk")
    document=json.loads(raw[20:20+length]);original=json.loads(json.dumps(document))
    binaryTail=raw[20+length:] # Preserve all later chunks byte for byte, including animation/skin/geometry data.
    changes=[]
    for material in document.get("materials",[]):
        name=material.get("name","")
        if name not in palette:raise RuntimeError("No matching source palette for "+name)
        source=palette[name]
        if source.get("retainExportedEmission"):
            if "emissiveFactor" not in material:
                raise RuntimeError("Expected existing exported emission for "+name)
            changes.append({"material":name,"retainedExportedEmission":True})
            continue
        pbr=material.setdefault("pbrMetallicRoughness",{})
        if "baseColorTexture" in pbr:raise RuntimeError("Refusing to flatten a textured material: "+name)
        old=pbr.get("baseColorFactor",[1,1,1,1])
        pbr["baseColorFactor"]=source["rgbaLinear"];pbr["metallicFactor"]=source["metallic"];pbr["roughnessFactor"]=source["roughness"]
        changes.append({"material":name,"beforeBaseColorFactor":old,"afterBaseColorFactor":source["rgbaLinear"]})
    a=dict(original);b=dict(document);a.pop("materials",None);b.pop("materials",None)
    if a!=b:raise RuntimeError("Non-material glTF data changed")
    data=json.dumps(document,separators=(",",":"),ensure_ascii=True).encode("utf-8")
    data+=b" "*((-len(data))%4)
    payload=struct.pack("<II",len(data),0x4e4f534a)+data+binaryTail
    result=struct.pack("<III",0x46546c67,2,len(payload)+12)+payload
    destination=Path(output) if output else path
    if output and destination.exists():raise RuntimeError("Existing corrected candidate preserved")
    destination.write_bytes(result)
    reread=destination.read_bytes();newlen=struct.unpack_from("<I",reread,12)[0]
    if reread[20+newlen:]!=binaryTail:raise RuntimeError("Binary chunks changed")
    return {"sourceFile":path.name,"beforeSha256":hashlib.sha256(raw).hexdigest(),"afterSha256":hashlib.sha256(result).hexdigest(),
            "nonMaterialJsonUnchanged":True,"binaryChunksUnchanged":True,"binaryChunksSha256":hashlib.sha256(binaryTail).hexdigest(),
            "changes":changes}

