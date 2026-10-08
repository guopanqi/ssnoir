const svg=body=>`<svg viewBox="0 0 1280 720" aria-hidden="true">${body}</svg>`;
const letter=`<g transform="translate(640 358)"><g class="letter"><path d="M-50-32H50V32H-50Z" fill="#b8b0a0" stroke="#757975" stroke-width="2"/><path d="m-50-32 50 33 50-33M-50 32l35-23m65 23L15 9" fill="none" stroke="#797d7b" stroke-width="2"/></g></g>`;
export function scenery(kind){
 let body='';
 if(kind==='street')body=`<ellipse cx="640" cy="586" rx="150" ry="15" fill="#bcb194" opacity=".08"/><path d="M639 251V583" stroke="#64606b" stroke-width="6"/><path d="M618 256H662L650 232H630Z" fill="#484350" stroke="#79747f" stroke-width="1.5"/><path class="lamp-cone" d="M632 260H648L780 582H500Z" fill="#e5c58c" opacity=".1"/><circle class="bulb" cx="640" cy="260" r="7" fill="#decdad"/>`;
 else if(kind==='door')body=`<path d="M945 190H1100V582H945Z" fill="#24303b" stroke="#59616b" stroke-width="4"/><path d="M961 208H1084V385H961Z M961 408H1084V563H961Z" fill="#202a34" stroke="#434e59" stroke-width="2"/><circle cx="1078" cy="399" r="5" fill="#b39c6b"/><rect x="893" y="300" width="34" height="90" rx="3" fill="#323f4c" stroke="#657282" stroke-width="2"/><path d="M903 313h14m-14 6h14m-14 6h14" stroke="#758293" stroke-width="1.5"/><circle cx="910" cy="363" r="5" fill="#ab966b"/>`;
 else if(kind==='room')body=`<path d="M193 469H400V483H193Z M208 483H217V582H208Z M375 483H384V582H375Z" fill="#574b3d" stroke="#81715d" stroke-width="1.5"/><path d="M318 466V398L294 379" fill="none" stroke="#737575" stroke-width="4"/><path d="M279 380H311L302 361H289Z" fill="#b5a078"/><path d="M284 382H307L344 468H240Z" fill="#deb77b" opacity=".1"/><path d="M960 444H1058V472H960Z M956 472H1062V526H956Z M965 525V583 M1053 525V583" fill="#4b4848" stroke="#777071" stroke-width="3"/>`;
 else throw Error(`Unknown set ${kind}`);
 return svg(body+letter);
}
