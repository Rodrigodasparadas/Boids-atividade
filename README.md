

# **Projeto Boids**

**Integrantes: Gabriel de Campos e Rodrigo Mamede**

**Como abrir a cena de teste?**    
Para abrir a cena principal, basta localizar a pasta **Scenes** e clicar em **MainScene**.

**Parâmetros**

| Nome  | Valor |
| ------------- | ------------- |
| Boid Count  | 150  |
| Spawn Radius  | 8  |
| Separation Weight  | 1.5  |
| Alignment Weight  | 1 |
| Cohesion Weight  | 1 |
| Perception Radius  | 2.5 |
| Separation Radius | 1.2 |
| Max Speed | 5  |
| Min Speed | 2  |
| Max Force | 10  |
| Turn Speed | 10  |  


## **Registros da parte B**

1) **Peso da separação = 0: o que sobra do bando?**  
 O bando acaba se juntando em seu centro e formam uma única massa, assim o bando desaparece


2) **Peso do alinhamento = 0: o que acontece agora?**  
 Ao zerar o peso do alinhamento os boids acabam não se movendo ao redor da área delimitada por conta propria e acabam apenas sendo atráidos uns pelos outros.

3) **Raio de percepção 0,5 versus 5,0: como o formato do bando muda?**  
 Com o raio de tamanho 0,5 os boids se disperçam e raramente se afetam, já com o raio 5,0, os boids ficam bem proximos uns dos outros e não se separam.

4) **Predador: adicione uma esfera que persegue o bando e um comportamento de fuga com peso alto no ComputeForce. Descreva o efeito causado no grupo.**  
 Quando o predador está perto, o grupo se disperça e sai de seu curso atual para se afastar do predador, e ficam assim até o predador se afastar.


## **Os vídeos estão na pasta Vídeos Teste**


