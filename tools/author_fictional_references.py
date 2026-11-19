"""Independent Decimal oracle for this original, fictional policy only.
Run explicitly when authoring the dataset; tests never rewrite expectations.
This is not a real-world tax calculator and imports no application code.
"""
from decimal import Decimal as D, ROUND_HALF_UP, localcontext
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
path=root/'data/portfolio-demo.json'
data=json.loads(path.read_text(),parse_float=D,parse_int=D)
vehicles={v['id']:v for v in data['vehicles']};roles={o['name']:o for o in data['occupations']}
def rounded(x):return int(x.quantize(D(1),rounding=ROUND_HALF_UP))
def tax(x):
 result=D(0);lower=D(0)
 for upper,rate in data['tax']['incomeBrackets']:
  part=max(D(0),min(x,upper) - lower) if upper is not None else max(D(0),x-lower)
  result+=part*rate
  if upper is None:break
  lower=upper
 return max(D(0),result-D(200))
def calculate(employee,car_id):
 role=roles[employee['occupation']];gross=sum(employee[k] for k in ['base','commission','bonus']);car=vehicles.get(car_id)
 maintenance=role['allowance'] if car is None else D(0);value=car['value'] if car else D(0)
 share=max(D(0),car['cost']-data['budgets'][role['group']])*data['family']['employeeGapShare'] if car and car.get('family') else D(0)
 participation=(car['participation'] if car else D(0))+share
 target=data['electricNetBonus'] if car and car['drive']=='3' else D(0)
 baseline=gross+maintenance+value
 # Independently invert progressive tax + 3% insurance by bisection.
 lo,hi=D(0),D(2000)
 for _ in range(180):
  m=(lo+hi)/2;gain=m-(tax(baseline+m)-tax(baseline))-m*D('.03')
  if gain<target:lo=m
  else:hi=m
 electric=(lo+hi)/2 if target else D(0)
 taxable=baseline+electric;income=tax(taxable);insurance=taxable*D('.03')
 net=gross+maintenance+electric-income-insurance-gross*D('.06')-participation
 return dict(net=rounded(net),tax=rounded(income),insurance=rounded(insurance),electric=rounded(electric),share=int(share))
with localcontext() as ctx:
 ctx.prec=50
 refs=[]
 for employee in data['employees']:
  c=calculate(employee,employee['current']);a=calculate(employee,employee['alternative'])
  refs.append(dict(employeeId=employee['id'],current=employee['current'],alternative=employee['alternative'],currentNet=c['net'],alternativeNet=a['net'],difference=a['net']-c['net'],currentTax=c['tax'],alternativeTax=a['tax'],currentInsurance=c['insurance'],alternativeInsurance=a['insurance'],electricGross=a['electric'],familyShare=a['share']))
# Preserve deliberately authored integers/rates, not Decimal JSON strings.
original=json.loads(path.read_text());original['references']=refs
path.write_text(json.dumps(original,ensure_ascii=False,indent=2)+'\n')
for r in refs:print(r['employeeId'],r['currentNet'],r['alternativeNet'],r['difference'])
