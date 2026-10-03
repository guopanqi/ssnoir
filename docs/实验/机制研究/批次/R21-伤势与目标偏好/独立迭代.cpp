// 独立定长状态表：不调用Python最优值/动作/转移；只共用正式判定概率输入。
#include <array>
#include <vector>
#include <map>
#include <iostream>
#include <iomanip>
#include <cmath>
#include <algorithm>
#include <stdexcept>
using V=std::array<double,4>;
struct Hand {std::vector<int> dice;double weight;std::vector<std::pair<int,int>> remove;};
std::vector<Hand> hands;std::map<std::vector<int>,int> ids;
std::array<std::array<V,7>,2> odds;
const int TOTAL=24*6*10*210;
int index(int a,int b,int c,int k,int h){return ((((a*4+b)*6+c)*10+k)*210+h);}
int severity(int z){return z==0?0:z<=6?(z+1)/2:z-3;}
bool head(int z){return z>0&&z<=6&&z%2==0;}
int health(int k,bool q){return k==0?0:k<=3?2*k-1+int(q):k+3;}
struct Branch {double p;int c,z;};
std::vector<Branch> debit(int c,int z){
 if(c>0)return {{1.,c-1,z}};
 int k=severity(z)+1;
 if(k==7)return {{1.,0,10}};
 if(z==0)return {{.25,0,health(1,true)},{.75,0,health(1,false)}};
 return {{1.,0,health(k,head(z))}};
}
V injuryFail(int z){return {0.,double(z!=0),0.,0.};}
void add(V &out,const V& v,double w){for(int j=0;j<4;++j)out[j]+=w*v[j];}
bool secondary(const V&a,const V&b){if(a[1]!=b[1])return a[1]<b[1];if(a[2]!=b[2])return a[2]<b[2];return a[3]>b[3];}
V select(const std::vector<V>& candidates,double lambda){
 double best=-100;for(const auto &v:candidates)best=std::max(best,v[0]-lambda*v[1]);
 std::vector<V> band;
 for(const auto &v:candidates)if(best-(v[0]-lambda*v[1])<=1e-12)band.push_back(v);
 for(int j:{1,2}) {double low=100;for(const auto &v:band)low=std::min(low,v[j]);band.erase(std::remove_if(band.begin(),band.end(),[&](const V&v){return v[j]-low>1e-12;}),band.end());}
 V ans=band.front();for(const auto &v:band)if(v[3]>ans[3])ans=v;
 return ans;
}
void makeHands(int n,int low,std::vector<int>& h){
 if((int)h.size()==n){double weight=1.;int factorial=1;for(int j=2;j<=n;++j)factorial*=j;weight=factorial/std::pow(6.,n);
  for(int d=1;d<=6;++d){int count=std::count(h.begin(),h.end(),d);for(int j=2;j<=count;++j)weight/=j;}
  ids[h]=hands.size();hands.push_back({h,weight,{}});return;}
 for(int d=low;d<=6;++d){h.push_back(d);makeHands(n,d,h);h.pop_back();}
}
int main(){
 for(int g=0;g<2;++g)for(int d=1;d<=6;++d)for(int j=0;j<3;++j)if(!(std::cin>>odds[g][d][j]))throw std::runtime_error("缺判定概率输入");
 std::vector<int> h;for(int n=0;n<=4;++n)makeHands(n,1,h);if(hands.size()!=210)throw std::runtime_error("手牌枚举异常");
 for(auto &hand:hands)for(int d=1;d<=6;++d){auto it=std::find(hand.dice.begin(),hand.dice.end(),d);if(it!=hand.dice.end()){auto rest=hand.dice;rest.erase(rest.begin()+(it-hand.dice.begin()));hand.remove.push_back({d,ids.at(rest)});}}
 std::cout<<std::setprecision(17)<<"{\"profiles\":[";bool first=true;
 for(bool reset:{true,false})for(double lambda:{0.,.25,1.}){
  std::vector<V> previous(TOTAL),current(TOTAL);V aggregate{};
  for(int t=1;t<=3;++t){
   // 回合结束先收时间税；下一层已有所有健康/手牌状态。
   std::array<V,24*6*10> endValues;
   for(int a=0;a<=5;++a)for(int b=0;b<=3;++b)for(int c=0;c<=5;++c)for(int z=0;z<10;++z){
    V end{};for(auto branch:debit(c,z)){
     if(branch.z==10||t==1)add(end,injuryFail(branch.z),branch.p);
     else {int aa=reset&&a?5:a,bb=reset&&b?3:b,n=severity(branch.z)>=4?3:4;
      for(int hi=0;hi<210;++hi)if((int)hands[hi].dice.size()==n)add(end,previous[index(aa,bb,branch.c,branch.z,hi)],branch.p*hands[hi].weight);}
    }endValues[index(a,b,c,z,0)/210]=end;
   }
   // 手牌按骰数递增；每个动作严格减少一骰。
   for(int hi=0;hi<210;++hi)for(int a=0;a<=5;++a)for(int b=0;b<=3;++b)for(int c=0;c<=5;++c)for(int z=0;z<10;++z){
    if(a==0&&b==0){current[index(a,b,c,z,hi)]={1.,double(z!=0),double(severity(z)),double(c)};continue;}
    std::vector<V> candidates{endValues[index(a,b,c,z,0)/210]};
    for(int route=0;route<3;++route){if((route==0&&a==0)||(route==1&&b==0)||(route==2&&c==5))continue;
     for(int act=0;act<(route==2?1:2);++act)for(auto [d,rest]:hands[hi].remove){
      V result{};for(int grade=0;grade<3;++grade){double w=odds[head(z)?0:1][d][grade];if(w==0)continue;
       int gain=route==2?0:act==0?(grade==2?1:0):grade;
       int dc=grade==0?-1:route==2?(grade==2?2:0):act==1&&grade==1?-1:0;
       auto branches=dc<0?debit(c,z):std::vector<Branch>{{1.,std::min(5,c+dc),z}};
       for(auto branch:branches){if(branch.z==10)add(result,injuryFail(10),w*branch.p);
        else{int aa=route==0?std::max(0,a-gain):a,bb=route==1?std::max(0,b-gain):b;add(result,current[index(aa,bb,branch.c,branch.z,rest)],w*branch.p);}}
      }candidates.push_back(result);
     }
    }current[index(a,b,c,z,hi)]=select(candidates,lambda);
   }
   current.swap(previous);
  }
  for(int hi=0;hi<210;++hi)if(hands[hi].dice.size()==4)add(aggregate,previous[index(5,3,3,0,hi)],hands[hi].weight);
  if(!first)std::cout<<",";first=false;std::cout<<"{\"reset\":"<<(reset?"true":"false")<<",\"cost\":"<<lambda<<",\"value\":[";
  for(int j=0;j<4;++j){if(j)std::cout<<",";std::cout<<aggregate[j];}
  std::cout<<"],\"hands\":[";bool fh=true;
  for(int hi=0;hi<210;++hi)if(hands[hi].dice.size()==4){if(!fh)std::cout<<",";fh=false;std::cout<<"{\"hand\":[";for(int j=0;j<4;++j){if(j)std::cout<<",";std::cout<<hands[hi].dice[j];}std::cout<<"],\"value\":[";for(int j=0;j<4;++j){if(j)std::cout<<",";std::cout<<previous[index(5,3,3,0,hi)][j];}std::cout<<"]}";}
  std::cout<<"]}";std::cerr<<"finished reset="<<reset<<" lambda="<<lambda<<" states="<<TOTAL*3<<"\n";
 }
 std::cout<<"]}\n";
}
