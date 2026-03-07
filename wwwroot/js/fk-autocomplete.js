(function(){
    function escHtml(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
    function highlight(text,filter){
        if(!filter)return escHtml(text);
        var idx=text.toLowerCase().indexOf(filter.toLowerCase());
        if(idx===-1)return escHtml(text);
        return escHtml(text.slice(0,idx))+'<strong>'+escHtml(text.slice(idx,idx+filter.length))+'</strong>'+escHtml(text.slice(idx+filter.length));
    }
    document.querySelectorAll('.fk-autocomplete').forEach(function(input){
        var target=document.getElementById(input.dataset.target);
        var dropdown=document.getElementById('dropdown_'+input.dataset.target);
        var all=Array.from(dropdown.querySelectorAll('.fk-option'));
        var activeIdx=-1;
        dropdown.style.display='none';
        function visible(){return all.filter(function(o){return o.style.display!=='none';});}
        function applyFilter(text){
            var f=text.toLowerCase();
            all.forEach(function(o){
                var match=!f||o.dataset.text.toLowerCase().includes(f);
                o.style.display=match?'':'none';
                if(match)o.innerHTML=highlight(o.dataset.text,text);
            });
            activeIdx=-1;setActive(-1);
        }
        function open(text){applyFilter(text);dropdown.style.display=visible().length?'block':'none';}
        function close(){dropdown.style.display='none';activeIdx=-1;}
        function setActive(idx){visible().forEach(function(o,i){o.classList.toggle('fk-active',i===idx);});activeIdx=idx;}
        function select(option){target.value=option.dataset.id;input.value=option.dataset.text;close();}
        input.addEventListener('focus',function(){open(this.value);});
        input.addEventListener('input',function(){target.value='';open(this.value);});
        input.addEventListener('keydown',function(e){
            var vis=visible();
            if(dropdown.style.display==='none'){if(e.key==='ArrowDown'){e.preventDefault();open(this.value);}return;}
            if(e.key==='ArrowDown'){e.preventDefault();setActive(Math.min(activeIdx+1,vis.length-1));}
            else if(e.key==='ArrowUp'){e.preventDefault();setActive(Math.max(activeIdx-1,0));}
            else if(e.key==='Enter'){if(activeIdx>=0&&vis[activeIdx]){e.preventDefault();select(vis[activeIdx]);}}
            else if(e.key==='Escape'){close();}
        });
        all.forEach(function(option){
            option.addEventListener('mousedown',function(e){e.preventDefault();select(option);});
            option.addEventListener('mouseover',function(){setActive(visible().indexOf(option));});
        });
        document.addEventListener('click',function(e){
            if(!input.contains(e.target)&&!dropdown.contains(e.target)){close();if(!target.value)input.value='';}
        });
    });
}());
