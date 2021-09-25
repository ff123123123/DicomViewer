private void Otsu_algorithm (int *input_image,int imageWidth,int imageHeight){


int N = imageWidth*imageHeight;
float threshold, var_max, sum, sumB, q1, q2, µ1, µ2 = 0;
int max_intensity = 255;
int histogram [255];
int i,value,Sigmb;


for (i=0; i<max_intensity;i++){
 histogram[i]=0;
}

for (i=0; i<N;i++){
value=input_image[i];
histogram[value]+=1;

for (i=0; i<max_intensity;i++){
 sum+= histogram[i];


for (t=0; t<max_intensity;t++){
  q1+=histogram[t];
  //if (q1==0) continue;
   q2=N-q1;

   sumB += t * histogram[t];
   µ1 = sumB/q1;
   µ2 = (sum - sumB)/q2;

   Sigmb = q1*q2*(µ1 - µ2)*(µ1 - µ2);
   if (Sigmb>var_max) { threshold=t; var_max=Sigmb;}
}

for (i=0; i<N;i++){
 if (input_image[i]>threshold)
  input_image[i]=255;
else
  input_image[i]=0;
}

}