__author__ = 'user'
import matplotlib.pyplot as plt

def drawGraph(path):
    file = open(path)

    values1 = []
    values2 = []
    values3 = []

    for item in file:
        temp = item.split(',')
        values1.append(int(temp[0]))
        values2.append(float(temp[1]))
        values3.append(float(temp[2]))
        #print(item)

    #print(values2)
    plt.figure(figsize=(10,8))
    plt.tick_params(labelsize=20)
    plt.plot(values1, values2, linewidth=3, linestyle = "-",color = 'green', ms=10, markevery=100 )
    plt.plot(values1, values3, linewidth=3, linestyle='--', color = 'red', ms=10, markevery=100)

#   set size of the legend like this: 'size':number
    plt.legend(['adaptive r$_{step}$', 'fixed r$_{step}$'], loc=1, prop={'size':24})

    #plt.plot(values1, values2)
    #plt.plot(values1, values2)
 #   plt.title("5242 nodes, 28980 edges", fontsize=26, y=1.02)
    plt.title("9877 nodes, 51971 edges", fontsize=26, y=1.02)
    plt.xlabel('Iteration #', fontsize=26)
    plt.ylabel('Step size', fontsize=26)
    plt.xlim(0, 1800)
    plt.legend(loc=0)
    plt.grid()
    plt.show()
  #  plt.savefig("C:\\egorov\\Procedia\\small_step.png")

basepath = "C:\\egorov\\energy characteristics\\stepData\\graphs\\"
#drawGraph(basepath + "stepsize_grqc_auto.csv")
drawGraph(basepath + "stepsize_hepth_compar.csv")