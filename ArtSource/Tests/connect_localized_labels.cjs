// Mechanical migration of text setters to explicit localization bindings.
// Run once, then use StationLocalization.Set directly for new labels.
const fs = require('fs');
const path = require('path');
function walk(dir) {
  return fs.readdirSync(dir, {withFileTypes:true}).flatMap(x =>
    x.isDirectory() ? walk(path.join(dir,x.name)) : [path.join(dir,x.name)]);
}
for (const file of walk('Assets/scripts')) {
  if (!file.endsWith('.cs') || /StationLocal/.test(file)) continue;
  const input=fs.readFileSync(file,'utf8');
  const pattern=/\b([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\.text\s*=(?!=)\s*/g;
  let result='', last=0, count=0, m;
  while ((m=pattern.exec(input))) {
    let end=pattern.lastIndex, quote=null;
    for (;end<input.length;end++) {
      const c=input[end];
      if (quote) { if(c==='\\'){end++;continue;} if(c===quote)quote=null; }
      else if(c==='"'||c==="'") quote=c;
      else if(c===';') break;
    }
    result+=input.slice(last,m.index)+`StationLocalization.Set(${m[1]}, ${input.slice(pattern.lastIndex,end)});`;
    last=end+1; pattern.lastIndex=last; count++;
  }
  if(count){fs.writeFileSync(file,result+input.slice(last)); console.log(file+': '+count+' bindings');}
}
